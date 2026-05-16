using BookingService.Messaging;
using Contracts.Events;

namespace BookingService.Handlers;

/// <summary>
/// Compensation step of the saga: payment failed, release the seat
/// (ticket -> CANCELLED) and publish <see cref="BookingCancelled"/>.
/// </summary>
public class PaymentFailedHandler
{
    private readonly IEventPublisher _publisher;
    private readonly ILogger<PaymentFailedHandler> _logger;

    public PaymentFailedHandler(IEventPublisher publisher, ILogger<PaymentFailedHandler> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public Task HandleAsync(PaymentFailed msg)
    {
        _logger.LogWarning("Payment failed for booking {BookingId}: {Reason}",
            msg.BookingId, msg.Reason);

        // -----------------------------------------------------------------
        // TODO: COMPENSATION - RELEASE SEAT
        // -----------------------------------------------------------------
        // Inside a transaction (optimistic locking):
        //   1. SELECT ticket WHERE id = msg.BookingId.
        //   2. If status == 'CONFIRMED' -> log inconsistency, abort.
        //      If status == 'CANCELLED' -> ignore (idempotent).
        //   3. UPDATE ticket SET status='CANCELLED', version=version+1
        //      WHERE id=@id AND version=@expectedVersion.
        //   4. COMMIT. The seat becomes free again because the unique
        //      partial index (showing_id, seat_id) WHERE status IN
        //      ('PENDING','CONFIRMED') no longer matches this row.
        // -----------------------------------------------------------------

        _publisher.Publish(new BookingCancelled(
            BookingId: msg.BookingId,
            Reason: msg.Reason,
            CancelledAtUtc: DateTime.UtcNow), BusTopology.BookingCancelled);

        return Task.CompletedTask;
    }
}

