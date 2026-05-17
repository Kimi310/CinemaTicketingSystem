using BookingService.Contracts;
using BookingService.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Tests;

[Collection(BookingFixtureCollection.Name)]
public class ConcurrencyTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    public ConcurrencyTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync() => _fx.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// The headline guarantee of the system: two requests racing for the
    /// same (showing_id, seat_id) MUST result in exactly one Created and
    /// one Conflict. This is what pessimistic locking + the unique index
    /// protect against.
    /// </summary>
    [Fact]
    public async Task Two_parallel_requests_for_same_seat_yield_one_created_and_one_conflict()
    {
        const int showingId = 1;
        const int seatId    = 42;

        var req1 = new CreateBookingRequest(showingId, seatId, UserId: 1, Amount: 25m, IdempotencyKey: Guid.NewGuid().ToString());
        var req2 = new CreateBookingRequest(showingId, seatId, UserId: 2, Amount: 25m, IdempotencyKey: Guid.NewGuid().ToString());

        // Fire both requests in parallel, each with its OWN DbContext (mimics two web threads).
        var t1 = Task.Run(async () =>
        {
            await using var db = _fx.CreateDbContext();
            return await _fx.CreateBookingService(db).CreateBookingAsync(req1);
        });
        var t2 = Task.Run(async () =>
        {
            await using var db = _fx.CreateDbContext();
            return await _fx.CreateBookingService(db).CreateBookingAsync(req2);
        });

        var results = await Task.WhenAll(t1, t2);

        results.Count(r => r.IsConflict).Should().Be(1, "exactly one of the two requests must lose the race");
        results.Count(r => !r.IsConflict && !r.IsIdempotentReplay).Should().Be(1, "exactly one must succeed");

        await using var verifyDb = _fx.CreateDbContext();
        var tickets = await verifyDb.Tickets.Where(t => t.ShowingId == showingId && t.SeatId == seatId).ToListAsync();
        tickets.Should().HaveCount(1, "the database must contain exactly one ticket for the contested seat");
        tickets[0].Status.Should().Be(TicketStatus.Pending);
    }

    /// <summary>
    /// Stress version of the above: N concurrent requests for the same seat
    /// must still yield exactly one winner. Exercises lock contention more
    /// aggressively than the 2-thread variant.
    /// </summary>
    [Fact]
    public async Task Ten_parallel_requests_for_same_seat_yield_exactly_one_winner()
    {
        const int showingId = 2;
        const int seatId    = 7;

        var tasks = Enumerable.Range(0, 10).Select(i => Task.Run(async () =>
        {
            await using var db = _fx.CreateDbContext();
            return await _fx.CreateBookingService(db).CreateBookingAsync(
                new CreateBookingRequest(showingId, seatId, UserId: i + 1, Amount: 25m, IdempotencyKey: Guid.NewGuid().ToString()));
        }));

        var results = await Task.WhenAll(tasks);

        results.Count(r => !r.IsConflict && !r.IsIdempotentReplay).Should().Be(1);
        results.Count(r => r.IsConflict).Should().Be(9);

        await using var verifyDb = _fx.CreateDbContext();
        (await verifyDb.Tickets.CountAsync(t => t.ShowingId == showingId && t.SeatId == seatId))
            .Should().Be(1);
    }
}

