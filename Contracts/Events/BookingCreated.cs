namespace Contracts.Events;


public record BookingCreated(
    Guid BookingId,
    int ShowingId,
    int SeatId,
    int UserId,
    decimal Amount,
    string IdempotencyKey,
    DateTime CreatedAtUtc);

