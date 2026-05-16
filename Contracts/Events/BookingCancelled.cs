namespace Contracts.Events;


public record BookingCancelled(
    Guid BookingId,
    string Reason,
    DateTime CancelledAtUtc);

