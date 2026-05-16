namespace Contracts.Events;


public record PaymentFailed(
    Guid BookingId,
    string Reason,
    DateTime ProcessedAtUtc);

