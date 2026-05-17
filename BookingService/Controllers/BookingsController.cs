using BookingService.Contracts;
using BookingService.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookingService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly ILogger<BookingsController> _logger;

    public BookingsController(IBookingService bookingService, ILogger<BookingsController> logger)
    {
        _bookingService = bookingService;
        _logger         = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateBookingResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(CreateBookingResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateBookingRequest request, CancellationToken ct)
    {
        var result = await _bookingService.CreateBookingAsync(request, ct);

        return result switch
        {
            { IsConflict: true }         => Conflict(new CreateBookingResponse(
                Guid.Empty, "CONFLICT", "Seat is already taken.")),

            { IsIdempotentReplay: true } => Accepted(new CreateBookingResponse(
                result.BookingId, "PENDING", "Booking already registered.")),

            // _ -> means default
            _                      => Accepted(new CreateBookingResponse(
                result.BookingId, "PENDING", "Seat reserved. You have 30 minutes to complete payment."))
        };
    }
}