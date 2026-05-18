using BookingService.Contracts;
using BookingService.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Tests;

[Collection(BookingFixtureCollection.Name)]
public class IdempotencyTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    public IdempotencyTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync() => _fx.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Replaying the same idempotency key (e.g. client retry after a network
    /// blip) must NOT create a second ticket — the first booking id is
    /// returned and the response is flagged as a replay.
    /// </summary>
    [Fact]
    public async Task Same_idempotency_key_returns_existing_booking_without_duplicating()
    {
        var key = Guid.NewGuid().ToString();
        var req = new CreateBookingRequest(ShowingId: 10, SeatId: 1, UserId: 1, Amount: 30m, IdempotencyKey: key);

        await using var db1 = _fx.CreateDbContext();
        var first = await _fx.CreateBookingService(db1).CreateBookingAsync(req);

        await using var db2 = _fx.CreateDbContext();
        var second = await _fx.CreateBookingService(db2).CreateBookingAsync(req);

        first.IsConflict.Should().BeFalse();
        second.IsIdempotentReplay.Should().BeTrue();
        second.BookingId.Should().Be(first.BookingId, "the second call must return the same booking id");

        await using var verifyDb = _fx.CreateDbContext();
        (await verifyDb.Tickets.CountAsync()).Should().Be(1, "no duplicate ticket may be created");
    }

    /// <summary>
    /// Different idempotency keys but the SAME seat must still fall back to
    /// the conflict path — idempotency only deduplicates true retries.
    /// </summary>
    [Fact]
    public async Task Different_keys_same_seat_still_produce_conflict()
    {
        var req1 = new CreateBookingRequest(11, 1, 1, 30m, Guid.NewGuid().ToString());
        var req2 = new CreateBookingRequest(11, 1, 2, 30m, Guid.NewGuid().ToString());

        await using var db1 = _fx.CreateDbContext();
        var r1 = await _fx.CreateBookingService(db1).CreateBookingAsync(req1);

        await using var db2 = _fx.CreateDbContext();
        var r2 = await _fx.CreateBookingService(db2).CreateBookingAsync(req2);

        r1.IsConflict.Should().BeFalse();
        r2.IsConflict.Should().BeTrue();
    }

    /// <summary>
    /// After a ticket is CANCELLED (compensation), the seat is free again
    /// and a NEW booking with a different key must succeed.
    /// </summary>
    [Fact]
    public async Task Cancelled_ticket_releases_the_seat_for_future_bookings()
    {
        var first = new CreateBookingRequest(12, 5, 1, 30m, Guid.NewGuid().ToString());

        await using (var db = _fx.CreateDbContext())
        {
            var r = await _fx.CreateBookingService(db).CreateBookingAsync(first);
            r.IsConflict.Should().BeFalse();

            // Simulate compensation: mark as CANCELLED.
            var t = await db.Tickets.FirstAsync(x => x.Id == r.BookingId);
            t.Status = TicketStatus.Cancelled;
            await db.SaveChangesAsync();
        }

        var second = new CreateBookingRequest(12, 5, 2, 30m, Guid.NewGuid().ToString());
        await using var db2 = _fx.CreateDbContext();
        var r2 = await _fx.CreateBookingService(db2).CreateBookingAsync(second);

        r2.IsConflict.Should().BeFalse("a cancelled ticket must not block new bookings for the same seat");
    }
}

