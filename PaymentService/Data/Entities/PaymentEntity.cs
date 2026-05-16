namespace PaymentService.Data.Entities;

public class PaymentEntity
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public string Status { get; set; } = PaymentStatus.Succeeded;
    public decimal? Amount { get; set; }
    public string? FailureReason { get; set; }
    public string IdempotencyKey { get; set; } = default!;
    public DateTime ProcessedAtUtc { get; set; }
}

public static class PaymentStatus
{
    public const string Succeeded = "SUCCEEDED";
    public const string Failed    = "FAILED";
}