namespace Contracts.Events;


public record BookingConfirmed(
    Guid BookingId,
    int ShowingId,
    int SeatId,
    int UserId,
    DateTime ConfirmedAtUtc);

