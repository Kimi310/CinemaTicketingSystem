using BookingService.Messaging;
using Contracts.Events;

namespace BookingService.Handlers;

/// <summary>
/// Reacts to a successful payment by confirming the booking
/// (ticket -> CONFIRMED) and publishing <see cref="BookingConfirmed"/>.
/// </summary>
public class PaymentSucceededHandler
{
    private readonly IEventPublisher _publisher;
    private readonly ILogger<PaymentSucceededHandler> _logger;

    public PaymentSucceededHandler(IEventPublisher publisher, ILogger<PaymentSucceededHandler> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public Task HandleAsync(PaymentSucceeded msg)
    {
        _logger.LogInformation("Payment {PaymentId} succeeded for booking {BookingId}",
            msg.PaymentId, msg.BookingId);

        // -----------------------------------------------------------------
        // TODO: CONFIRM BOOKING IN DB
        // -----------------------------------------------------------------
        // Inside a transaction:
        //   1. SELECT ticket WHERE id = msg.BookingId.
        //      Read its current `version` (optimistic locking column).
        //   2. If status != 'PENDING' -> ignore (idempotent: already
        //      processed or cancelled).
        //   3. UPDATE ticket SET status='CONFIRMED', version=version+1
        //      WHERE id=@id AND version=@expectedVersion.
        //      If 0 rows affected -> someone else updated it; retry/abort.
        //   4. COMMIT.
        //
        // Load showing_id / seat_id / user_id from the persisted ticket
        // rather than carrying them through the saga. For now we publish
        // placeholders.
        // -----------------------------------------------------------------

        _publisher.Publish(new BookingConfirmed(
            BookingId: msg.BookingId,
            ShowingId: 0,   // TODO: read from ticket row
            SeatId: 0,      // TODO: read from ticket row
            UserId: 0,      // TODO: read from ticket row
            ConfirmedAtUtc: DateTime.UtcNow), BusTopology.BookingConfirmed);

        return Task.CompletedTask;
    }
}

