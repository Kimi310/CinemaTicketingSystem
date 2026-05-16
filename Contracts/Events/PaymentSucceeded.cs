namespace Contracts.Events;


public record PaymentSucceeded(
    Guid BookingId,
    Guid PaymentId,
    decimal Amount,
    DateTime ProcessedAtUtc);

