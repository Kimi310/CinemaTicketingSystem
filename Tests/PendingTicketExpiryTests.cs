using BookingService.Data;
using BookingService.Data.Entities;
using BookingService.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookingService.Tests;

[Collection(BookingFixtureCollection.Name)]
public class PendingTicketExpiryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    public PendingTicketExpiryTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync() => _fx.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// The 30-minute safety net: a PENDING ticket older than the TTL must be
    /// flipped to CANCELLED, and a BookingCancelled outbox row must be
    /// produced so the rest of the system finds out eventually.
    /// A fresh PENDING ticket must remain untouched.
    /// </summary>
    [Fact]
    public async Task Expires_stale_pending_tickets_and_writes_outbox_event()
    {
        var staleId = Guid.NewGuid();
        var freshId = Guid.NewGuid();

        await using (var db = _fx.CreateDbContext())
        {
            db.Tickets.Add(new TicketEntity
            {
                Id = staleId, ShowingId = 50, SeatId = 1, UserId = 1,
                Status = TicketStatus.Pending,
                IdempotencyKey = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow - TimeSpan.FromMinutes(45), // > 30 min TTL
            });
            db.Tickets.Add(new TicketEntity
            {
                Id = freshId, ShowingId = 50, SeatId = 2, UserId = 2,
                Status = TicketStatus.Pending,
                IdempotencyKey = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow - TimeSpan.FromMinutes(5),
            });
            await db.SaveChangesAsync();
        }

        // Build a scope factory that hands out the test's BookingDbContext.
        var services = new ServiceCollection();
        services.AddDbContext<BookingDbContext>(o => o.UseNpgsql(_fx.ConnectionString));
        var sp = services.BuildServiceProvider();

        var worker = new PendingTicketExpiryService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PendingTicketExpiryService>.Instance);

        // Invoke the private expiry pass via the public BackgroundService.StartAsync would
        // start the long-running loop; instead trigger one iteration via reflection.
        var method = typeof(PendingTicketExpiryService).GetMethod("ExpireOnceAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        await (Task)method.Invoke(worker, new object[] { CancellationToken.None })!;

        await using var verifyDb = _fx.CreateDbContext();
        var stale = await verifyDb.Tickets.FirstAsync(t => t.Id == staleId);
        var fresh = await verifyDb.Tickets.FirstAsync(t => t.Id == freshId);

        stale.Status.Should().Be(TicketStatus.Cancelled, "ticket older than 30 minutes must be expired");
        fresh.Status.Should().Be(TicketStatus.Pending,   "ticket younger than 30 minutes must be left alone");

        var outbox = await verifyDb.OutboxBookingCancelled.Where(o => o.BookingId == staleId).ToListAsync();
        outbox.Should().ContainSingle("an outbox row must be written for each expired ticket");
        outbox[0].PublishedAtUtc.Should().BeNull("the relay (not the worker) flips PublishedAtUtc");
    }
}

