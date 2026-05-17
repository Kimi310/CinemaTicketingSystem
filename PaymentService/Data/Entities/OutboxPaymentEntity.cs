namespace PaymentService.Data.Entities;

/// <summary>
/// Transactional outbox row for payment outcomes. Written in the same
/// transaction as the PaymentEntity, then dispatched by OutboxRelayService.
/// Guarantees at-least-once publication of PaymentSucceeded / PaymentFailed,
/// even if the process dies right after writing the payment row.
/// </summary>
public class OutboxPaymentEntity
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }

    /// <summary>"PaymentSucceeded" or "PaymentFailed" (matches BusTopology routing key).</summary>
    public string EventType { get; set; } = default!;

    /// <summary>Serialized event payload (JSON).</summary>
    public string Payload { get; set; } = default!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; set; }
}

