namespace BookingService.Contracts;


public record CreateBookingRequest(
    int ShowingId,
    int SeatId,
    int UserId,
    decimal Amount,

    string IdempotencyKey);

public record CreateBookingResponse(
    Guid BookingId,
    string Status,
    string Message);

