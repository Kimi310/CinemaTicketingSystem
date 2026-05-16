namespace BookingService.Data.Entities;

public class OutboxBookingCreatedEntity
{
    public Guid Id { get; set; }             // same as BookingId / TicketEntity.Id
    public int ShowingId { get; set; }
    public int SeatId { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public string IdempotencyKey { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; set; }  // null = not yet dispatched
}
