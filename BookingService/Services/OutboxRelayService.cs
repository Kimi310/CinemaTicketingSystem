using BookingService.Data.Repositories;
using BookingService.Messaging;
using Contracts.Events;

namespace BookingService.Services;

// Service for making sure the events get published (publishes every 2.5 seconds)
public class OutboxRelayService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<OutboxRelayService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(2.5);

    public OutboxRelayService(
        IServiceScopeFactory scopeFactory,
        IEventPublisher publisher,
        ILogger<OutboxRelayService> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher    = publisher;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox relay started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessBatchAsync(stoppingToken);
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();

        try
        {
            var events = await repository.GetUnpublishedOutboxEventsAsync(batchSize: 50, ct);

            if (events.Count == 0) return;

            _logger.LogInformation("Outbox relay: dispatching {Count} event(s)", events.Count);

            foreach (var outboxEvent in events)
            {
                try
                {
                    _publisher.Publish(new BookingCreated(
                        BookingId:      outboxEvent.Id,
                        ShowingId:      outboxEvent.ShowingId,
                        SeatId:         outboxEvent.SeatId,
                        UserId:         outboxEvent.UserId,
                        Amount:         outboxEvent.Amount,
                        IdempotencyKey: outboxEvent.IdempotencyKey,
                        CreatedAtUtc:   outboxEvent.CreatedAtUtc),
                        BusTopology.BookingCreated);

                    await repository.MarkOutboxEventPublishedAsync(outboxEvent.Id, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish outbox event {EventId}", outboxEvent.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Outbox relay poll failed");
        }
    }
}