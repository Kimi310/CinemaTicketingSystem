using Contracts.Events;
using PaymentService.Data.Entities;
using PaymentService.Data.Repositories;
using PaymentService.Messaging;

namespace PaymentService.Handlers;

public class BookingCreatedHandler
{
    private static readonly Random Rng = new();

    private readonly IPaymentRepository _repository;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<BookingCreatedHandler> _logger;

    public BookingCreatedHandler(
        IPaymentRepository repository,
        IEventPublisher publisher,
        ILogger<BookingCreatedHandler> logger)
    {
        _repository = repository;
        _publisher  = publisher;
        _logger     = logger;
    }

    public async Task HandleAsync(BookingCreated msg)
    {
        _logger.LogInformation(
            "Processing payment for booking {BookingId} (amount {Amount})",
            msg.BookingId, msg.Amount);

        // Idempotency check!
        // If duplicate idempotency key, republish the previous outcome instead of charging the customer again.
        var existing = await _repository.FindByIdempotencyKeyAsync(msg.IdempotencyKey);
        if (existing is not null)
        {
            _logger.LogWarning("Duplicate BookingCreated for booking {BookingId} — republishing previous outcome", msg.BookingId);
            RepublishOutcome(existing, msg);
            return;
        }

        // Simulate payment processing since we cannot pay ourselves
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        var success = Rng.NextDouble() < 0.8;

        var payment = new PaymentEntity
        {
            Id             = Guid.NewGuid(),
            BookingId      = msg.BookingId,
            IdempotencyKey = msg.IdempotencyKey,
            ProcessedAtUtc = DateTime.UtcNow,
        };

        if (success)
        {
            payment.Status = PaymentStatus.Succeeded;
            payment.Amount = msg.Amount;

            await _repository.InsertAsync(payment);

            _logger.LogInformation("Payment SUCCEEDED for booking {BookingId}", msg.BookingId);

            _publisher.Publish(new PaymentSucceeded(
                BookingId:      msg.BookingId,
                PaymentId:      payment.Id,
                Amount:         msg.Amount,
                ProcessedAtUtc: payment.ProcessedAtUtc),
                BusTopology.PaymentSucceeded);
        }
        else
        {
            const string reason = "Simulated payment decline";
            payment.Status        = PaymentStatus.Failed;
            payment.FailureReason = reason;

            await _repository.InsertAsync(payment);

            _logger.LogWarning("Payment FAILED for booking {BookingId}: {Reason}", msg.BookingId, reason);

            _publisher.Publish(new PaymentFailed(
                BookingId:      msg.BookingId,
                Reason:         reason,
                ProcessedAtUtc: payment.ProcessedAtUtc),
                BusTopology.PaymentFailed);
        }
    }

    // Republish the outcome of a previously processed payment. (BookingService handlers are idempotent so its okay)
    private void RepublishOutcome(PaymentEntity existing, BookingCreated msg)
    {
        if (existing.Status == PaymentStatus.Succeeded)
        {
            _publisher.Publish(new PaymentSucceeded(
                BookingId:      existing.BookingId,
                PaymentId:      existing.Id,
                Amount:         existing.Amount!.Value,
                ProcessedAtUtc: existing.ProcessedAtUtc),
                BusTopology.PaymentSucceeded);
        }
        else
        {
            _publisher.Publish(new PaymentFailed(
                BookingId:      existing.BookingId,
                Reason:         existing.FailureReason ?? "Unknown",
                ProcessedAtUtc: existing.ProcessedAtUtc),
                BusTopology.PaymentFailed);
        }
    }
}