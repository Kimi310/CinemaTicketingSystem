using BookingService.Contracts;
using BookingService.Messaging;
using Contracts.Events;
using Microsoft.AspNetCore.Mvc;

namespace BookingService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly IEventPublisher _publisher;
    private readonly ILogger<BookingsController> _logger;

    public BookingsController(IEventPublisher publisher, ILogger<BookingsController> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }
    
    [HttpPost]
    [ProducesResponseType(typeof(CreateBookingResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Create([FromBody] CreateBookingRequest request)
    {
        var bookingId = Guid.NewGuid();

        // -----------------------------------------------------------------
        // TODO: PERSIST RESERVATION (Seat-locking flow from the diagram)
        // -----------------------------------------------------------------
        // Inside a single DB transaction:
        //   1. SELECT ... FOR UPDATE on SEAT row (request.SeatId)
        //      to take a row-level lock and prevent concurrent reservations
        //      for the same (showing_id, seat_id) pair.
        //   2. Check whether a TICKET already exists for
        //      (showing_id, seat_id) with status in (PENDING, CONFIRMED).
        //      If yes -> ROLLBACK and return 409 Conflict ("Seat just taken").
        //   3. Check idempotency: if a TICKET with the same
        //      idempotency_key exists -> return the existing booking
        //      (do NOT insert a duplicate, do NOT publish a new event).
        //   4. INSERT INTO ticket (id, showing_id, seat_id, user_id,
        //                          status='PENDING', idempotency_key,
        //                          version=0, created_at=NOW()).
        //   5. COMMIT (lock released).
        //
        // For real production use the Transactional Outbox pattern:
        // write the BookingCreated event to an outbox table in the SAME
        // transaction, then have a relay publish it to RabbitMQ. This
        // avoids the dual-write problem (DB committed but message lost,
        // or vice versa).
        // -----------------------------------------------------------------

        _logger.LogInformation(
            "Reserved seat {SeatId} for showing {ShowingId} (booking {BookingId})",
            request.SeatId, request.ShowingId, bookingId);

        _publisher.Publish(new BookingCreated(
            BookingId: bookingId,
            ShowingId: request.ShowingId,
            SeatId: request.SeatId,
            UserId: request.UserId,
            Amount: request.Amount,
            IdempotencyKey: request.IdempotencyKey,
            CreatedAtUtc: DateTime.UtcNow), BusTopology.BookingCreated);

        return Accepted(new CreateBookingResponse(
            bookingId,
            "PENDING",
            "Seat reserved. You have 30 minutes to complete the payment."));
    }
}
