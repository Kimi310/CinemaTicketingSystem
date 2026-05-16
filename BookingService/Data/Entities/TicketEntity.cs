namespace BookingService.Data.Entities;

public class TicketEntity
{
    public Guid Id { get; set; }
    public int ShowingId { get; set; }
    public int SeatId { get; set; }
    public int UserId { get; set; }
    public string Status { get; set; } = TicketStatus.Pending;
    public string IdempotencyKey { get; set; } = default!;
    public int Version { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
 
public static class TicketStatus
{
    public const string Pending   = "PENDING";
    public const string Confirmed = "CONFIRMED";
    public const string Cancelled = "CANCELLED";
}
