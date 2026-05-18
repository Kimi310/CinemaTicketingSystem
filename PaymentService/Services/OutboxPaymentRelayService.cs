using PaymentService.Data.Repositories;
using PaymentService.Messaging;

namespace PaymentService.Services;

/// <summary>
/// Polls the outbox_payment table and publishes pending events to RabbitMQ.
/// Ensures at-least-once delivery: if the handler dies after the DB commit
/// but before the broker publish, the next poll picks it up. Combined with
/// BookingService's idempotent PaymentSucceeded/PaymentFailed handlers this
/// yields eventual consistency between the payment DB and the saga state.
/// </summary>
public class OutboxPaymentRelayService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<OutboxPaymentRelayService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(2.5);

    public OutboxPaymentRelayService(
        IServiceScopeFactory scopeFactory,
        IEventPublisher publisher,
        ILogger<OutboxPaymentRelayService> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher    = publisher;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Payment outbox relay started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessBatchAsync(stoppingToken);
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();

        try
        {
            var events = await repo.GetUnpublishedOutboxAsync(batchSize: 50, ct);
            if (events.Count == 0) return;

            _logger.LogInformation("Payment outbox: dispatching {Count} event(s)", events.Count);

            foreach (var ob in events)
            {
                try
                {
                    // Publish raw JSON payload under the original routing key.
                    _publisher.PublishRaw(ob.Payload, ob.EventType);
                    await repo.MarkOutboxPublishedAsync(ob.Id, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish payment outbox event {EventId}", ob.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payment outbox poll failed");
        }
    }
}

