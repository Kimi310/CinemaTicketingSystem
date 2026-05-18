namespace BookingService.Data.Entities;

/// <summary>
/// Outbox row for BookingCancelled events emitted by the expiry worker
/// (and any other internal cancellations). Written atomically with the
/// ticket status transition PENDING -> CANCELLED.
/// </summary>
public class OutboxBookingCancelledEntity
{
    public Guid Id { get; set; }              // outbox row id
    public Guid BookingId { get; set; }
    public string Reason { get; set; } = default!;
    public DateTime CancelledAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; set; }
}

