using BookingService.Contracts;
 
namespace BookingService.Services;
 
public interface IBookingService
{
    Task<BookingResult> CreateBookingAsync(CreateBookingRequest request, CancellationToken ct = default);
}
 
public sealed class BookingResult
{
    public Guid BookingId          { get; private set; }
    public bool IsConflict         { get; private set; }
    public bool IsIdempotentReplay { get; private set; }
 
    public static BookingResult Created(Guid id) => new() { BookingId = id };
    public static BookingResult Conflict()        => new() { IsConflict = true };
    public static BookingResult Replayed(Guid id) => new() { BookingId = id, IsIdempotentReplay = true };
}