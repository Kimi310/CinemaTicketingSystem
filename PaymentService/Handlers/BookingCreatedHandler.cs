using Contracts.Events;
using PaymentService.Messaging;

namespace PaymentService.Handlers;

/// <summary>
/// Reacts to a <see cref="BookingCreated"/> event by simulating payment
/// and publishing either <see cref="PaymentSucceeded"/> or
/// <see cref="PaymentFailed"/>.
/// </summary>
public class BookingCreatedHandler
{
    private static readonly Random Rng = new();

    private readonly IEventPublisher _publisher;
    private readonly ILogger<BookingCreatedHandler> _logger;

    public BookingCreatedHandler(IEventPublisher publisher, ILogger<BookingCreatedHandler> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task HandleAsync(BookingCreated msg)
    {
        _logger.LogInformation(
            "Processing payment for booking {BookingId} (amount {Amount})",
            msg.BookingId, msg.Amount);

        // -----------------------------------------------------------------
        // TODO: PERSIST PAYMENT ATTEMPT
        // -----------------------------------------------------------------
        // Use msg.IdempotencyKey as the unique key for the PAYMENT row so
        // duplicate delivery of BookingCreated (at-least-once semantics
        // of RabbitMQ) doesn't charge the customer twice.
        //   1. INSERT INTO payment (id, booking_id, amount, status='PROCESSING',
        //                           idempotency_key) ON CONFLICT DO NOTHING.
        //   2. If conflict -> SELECT existing payment row and re-publish
        //      its previous outcome instead of charging again.
        // -----------------------------------------------------------------

        // Simulate latency + 80% success rate.
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        var success = Rng.NextDouble() < 0.8;

        if (success)
        {
            // TODO: UPDATE payment SET status='SUCCEEDED' WHERE id=...
            _logger.LogInformation("Payment SUCCEEDED for booking {BookingId}", msg.BookingId);
            _publisher.Publish(new PaymentSucceeded(
                BookingId: msg.BookingId,
                PaymentId: Guid.NewGuid(),
                Amount: msg.Amount,
                ProcessedAtUtc: DateTime.UtcNow), BusTopology.PaymentSucceeded);
        }
        else
        {
            // TODO: UPDATE payment SET status='FAILED', failure_reason=... WHERE id=...
            const string reason = "Simulated payment decline";
            _logger.LogWarning("Payment FAILED for booking {BookingId}: {Reason}",
                msg.BookingId, reason);
            _publisher.Publish(new PaymentFailed(
                BookingId: msg.BookingId,
                Reason: reason,
                ProcessedAtUtc: DateTime.UtcNow), BusTopology.PaymentFailed);
        }
    }
}

