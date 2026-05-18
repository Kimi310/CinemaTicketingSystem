using BookingService.Data.Entities;
using BookingService.Data.Repositories;
using BookingService.Handlers;
using BookingService.Messaging;
using Contracts.Events;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookingService.Tests;

[Collection(BookingFixtureCollection.Name)]
public class SagaHandlerTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    public SagaHandlerTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync() => _fx.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<TicketEntity> SeedPendingTicketAsync(int showingId, int seatId)
    {
        await using var db = _fx.CreateDbContext();
        var ticket = new TicketEntity
        {
            Id             = Guid.NewGuid(),
            ShowingId      = showingId,
            SeatId         = seatId,
            UserId         = 1,
            Status         = TicketStatus.Pending,
            IdempotencyKey = Guid.NewGuid().ToString(),
            CreatedAt      = DateTime.UtcNow,
        };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        return ticket;
    }

    /// <summary>
    /// Happy path of the saga: PaymentSucceeded flips PENDING -> CONFIRMED
    /// and emits exactly one BookingConfirmed event.
    /// </summary>
    [Fact]
    public async Task PaymentSucceeded_confirms_ticket_and_publishes_BookingConfirmed()
    {
        var ticket = await SeedPendingTicketAsync(100, 1);
        var publisher = new NullEventPublisher();

        await using var db = _fx.CreateDbContext();
        var repo = new BookingRepository(db, NullLogger<BookingRepository>.Instance);
        var handler = new PaymentSucceededHandler(repo, publisher, NullLogger<PaymentSucceededHandler>.Instance);

        await handler.HandleAsync(new PaymentSucceeded(ticket.Id, Guid.NewGuid(), 25m, DateTime.UtcNow));

        await using var verifyDb = _fx.CreateDbContext();
        var updated = await verifyDb.Tickets.FirstAsync(t => t.Id == ticket.Id);
        updated.Status.Should().Be(TicketStatus.Confirmed);
        publisher.Published.Should().ContainSingle(p => p.RoutingKey == BusTopology.BookingConfirmed);
    }

    /// <summary>
    /// At-least-once delivery means handlers WILL see duplicates. Replaying
    /// PaymentSucceeded for a ticket that is already CONFIRMED must not
    /// re-emit BookingConfirmed (otherwise the user gets two e-tickets).
    /// </summary>
    [Fact]
    public async Task PaymentSucceeded_is_idempotent_on_replay()
    {
        var ticket = await SeedPendingTicketAsync(101, 1);
        var publisher = new NullEventPublisher();

        async Task RunOnce()
        {
            await using var db = _fx.CreateDbContext();
            var repo = new BookingRepository(db, NullLogger<BookingRepository>.Instance);
            var handler = new PaymentSucceededHandler(repo, publisher, NullLogger<PaymentSucceededHandler>.Instance);
            await handler.HandleAsync(new PaymentSucceeded(ticket.Id, Guid.NewGuid(), 25m, DateTime.UtcNow));
        }

        await RunOnce();
        await RunOnce();   // duplicate delivery

        publisher.Published.Count(p => p.RoutingKey == BusTopology.BookingConfirmed)
            .Should().Be(1, "duplicate PaymentSucceeded must not republish BookingConfirmed");
    }

    /// <summary>
    /// Compensation path: PaymentFailed flips PENDING -> CANCELLED and
    /// publishes BookingCancelled so downstream services release resources.
    /// </summary>
    [Fact]
    public async Task PaymentFailed_cancels_ticket_and_publishes_BookingCancelled()
    {
        var ticket = await SeedPendingTicketAsync(102, 1);
        var publisher = new NullEventPublisher();

        await using var db = _fx.CreateDbContext();
        var repo = new BookingRepository(db, NullLogger<BookingRepository>.Instance);
        var handler = new PaymentFailedHandler(repo, publisher, NullLogger<PaymentFailedHandler>.Instance);

        await handler.HandleAsync(new PaymentFailed(ticket.Id, "card declined", DateTime.UtcNow));

        await using var verifyDb = _fx.CreateDbContext();
        var updated = await verifyDb.Tickets.FirstAsync(t => t.Id == ticket.Id);
        updated.Status.Should().Be(TicketStatus.Cancelled);
        publisher.Published.Should().ContainSingle(p => p.RoutingKey == BusTopology.BookingCancelled);
    }

    /// <summary>
    /// Safety net: a stray PaymentFailed arriving AFTER the ticket has been
    /// confirmed must not silently cancel a confirmed booking.
    /// </summary>
    [Fact]
    public async Task PaymentFailed_does_not_cancel_already_confirmed_ticket()
    {
        var ticket = await SeedPendingTicketAsync(103, 1);
        await using (var db = _fx.CreateDbContext())
        {
            var t = await db.Tickets.FirstAsync(x => x.Id == ticket.Id);
            t.Status = TicketStatus.Confirmed;
            await db.SaveChangesAsync();
        }

        var publisher = new NullEventPublisher();
        await using var db2 = _fx.CreateDbContext();
        var repo = new BookingRepository(db2, NullLogger<BookingRepository>.Instance);
        var handler = new PaymentFailedHandler(repo, publisher, NullLogger<PaymentFailedHandler>.Instance);

        await handler.HandleAsync(new PaymentFailed(ticket.Id, "late failure", DateTime.UtcNow));

        await using var verifyDb = _fx.CreateDbContext();
        var unchanged = await verifyDb.Tickets.FirstAsync(t => t.Id == ticket.Id);
        unchanged.Status.Should().Be(TicketStatus.Confirmed);
        publisher.Published.Should().BeEmpty();
    }
}

