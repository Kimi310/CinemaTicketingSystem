using BookingService.Data.Entities;

namespace BookingService.Data.Repositories;

public interface IBookingRepository
{
    /// <summary>
    /// Looks up a ticket by its primary key. Returns null if not found.
    /// </summary>
    Task<TicketEntity?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Looks up an existing ticket by idempotency key.
    /// Returns null if no matching ticket exists.
    /// </summary>
    Task<TicketEntity?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);

    /// <summary>
    /// Returns true if a PENDING or CONFIRMED ticket already exists
    /// for the given (showingId, seatId) pair.
    /// </summary>
    Task<bool> SeatIsTakenAsync(int showingId, int seatId, CancellationToken ct = default);

    /// <summary>
    /// Atomically inserts the ticket and its outbox event in a single transaction.
    /// Throws DbUpdateException on unique constraint violations (seat conflict or
    /// duplicate idempotency key race).
    /// </summary>
    Task CreateBookingAsync(TicketEntity ticket, OutboxBookingCreatedEntity outboxEvent, CancellationToken ct = default);

    /// <summary>
    /// Updates ticket status (CONFIRMED / CANCELLED) using optimistic locking.
    /// Throws DbUpdateConcurrencyException if the version has changed.
    /// </summary>
    Task UpdateTicketStatusAsync(Guid ticketId, string newStatus, CancellationToken ct = default);

    /// <summary>
    /// Returns a batch of unpublished outbox events for the relay to dispatch.
    /// </summary>
    Task<IReadOnlyList<OutboxBookingCreatedEntity>> GetUnpublishedOutboxEventsAsync(int batchSize = 50, CancellationToken ct = default);

    /// <summary>
    /// Marks an outbox event as published so the relay does not re-dispatch it.
    /// </summary>
    Task MarkOutboxEventPublishedAsync(Guid outboxEventId, CancellationToken ct = default);
}