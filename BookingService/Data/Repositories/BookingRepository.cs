using BookingService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Data.Repositories;

public class BookingRepository : IBookingRepository
{
    private readonly BookingDbContext _db;
    private readonly ILogger<BookingRepository> _logger;

    public BookingRepository(BookingDbContext db, ILogger<BookingRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── Lookups ───────────────────────────────────────────────────────────

    public Task<TicketEntity?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Tickets
              .AsNoTracking()
              .FirstOrDefaultAsync(t => t.Id == id, ct);

    // ── Idempotency check ─────────────────────────────────────────────────

    public Task<TicketEntity?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
        => _db.Tickets
              .AsNoTracking()
              .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, ct);

    // ── Seat availability ─────────────────────────────────────────────────

    public Task<bool> SeatIsTakenAsync(int showingId, int seatId, CancellationToken ct = default)
        => _db.Tickets.AnyAsync(
               t => t.ShowingId == showingId
                 && t.SeatId == seatId
                 && (t.Status == TicketStatus.Pending || t.Status == TicketStatus.Confirmed),
               ct);

    // ── Write: ticket + outbox (single transaction) ───────────────────────

    public async Task CreateBookingAsync(
        TicketEntity ticket,
        OutboxBookingCreatedEntity outboxEvent,
        CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            _db.Tickets.Add(ticket);
            _db.OutboxBookingCreated.Add(outboxEvent);

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Booking {BookingId} persisted with outbox event (showing={ShowingId}, seat={SeatId})",
                ticket.Id, ticket.ShowingId, ticket.SeatId);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    // ── Write: status update with optimistic locking ──────────────────────

    public async Task UpdateTicketStatusAsync(Guid ticketId, string newStatus, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FindAsync([ticketId], ct)
            ?? throw new InvalidOperationException($"Ticket {ticketId} not found.");

        ticket.Status = newStatus;

        // EF will throw DbUpdateConcurrencyException if the row was
        // modified by another process between the read and this write.
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Ticket {TicketId} status updated to {Status}", ticketId, newStatus);
    }

    // Outbox relay support

    public Task<IReadOnlyList<OutboxBookingCreatedEntity>> GetUnpublishedOutboxEventsAsync(
        int batchSize = 50,
        CancellationToken ct = default)
        => _db.OutboxBookingCreated
              .Where(o => o.PublishedAtUtc == null)
              .OrderBy(o => o.CreatedAtUtc)
              .Take(batchSize)
              .AsNoTracking()
              .ToListAsync(ct)
              .ContinueWith(t => (IReadOnlyList<OutboxBookingCreatedEntity>)t.Result, ct);

    public async Task MarkOutboxEventPublishedAsync(Guid outboxEventId, CancellationToken ct = default)
    {
        await _db.OutboxBookingCreated
                 .Where(o => o.Id == outboxEventId)
                 .ExecuteUpdateAsync(
                     s => s.SetProperty(o => o.PublishedAtUtc, DateTime.UtcNow),
                     ct);

        _logger.LogInformation("Outbox event {OutboxEventId} marked as published", outboxEventId);
    }
}