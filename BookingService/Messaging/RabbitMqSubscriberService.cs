using System.Text;
using System.Text.Json;
using BookingService.Handlers;
using Contracts.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BookingService.Messaging;

/// <summary>
/// BackgroundService that owns the consumer channel for BookingService.
/// Declares a single durable queue bound to <see cref="BusTopology.Exchange"/>
/// for the routing keys this service cares about, then dispatches each
/// message to the matching handler.
/// </summary>
public sealed class RabbitMqSubscriberService : BackgroundService
{
    private const string QueueName = "booking-service.queue";

    private readonly RabbitMqConnection _connection;
    private readonly IServiceProvider _services;
    private readonly ILogger<RabbitMqSubscriberService> _logger;
    private IModel? _channel;

    public RabbitMqSubscriberService(
        RabbitMqConnection connection,
        IServiceProvider services,
        ILogger<RabbitMqSubscriberService> logger)
    {
        _connection = connection;
        _services = services;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = _connection.Connection.CreateModel();

        // Fair dispatch: don't push a new message to a consumer that still
        // has one in-flight (so slow handlers don't starve other instances).
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

        _channel.QueueDeclare(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        // Subscribe only to the events BookingService reacts to.
        _channel.QueueBind(QueueName, BusTopology.Exchange, BusTopology.PaymentSucceeded);
        _channel.QueueBind(QueueName, BusTopology.Exchange, BusTopology.PaymentFailed);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnMessage;

        _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);

        _logger.LogInformation("BookingService subscriber started on queue {Queue}", QueueName);
        return Task.CompletedTask;
    }

    private async Task OnMessage(object sender, BasicDeliverEventArgs ea)
    {
        var routingKey = ea.RoutingKey;
        var json = Encoding.UTF8.GetString(ea.Body.Span);

        try
        {
            using var scope = _services.CreateScope();

            switch (routingKey)
            {
                case BusTopology.PaymentSucceeded:
                {
                    var evt = JsonSerializer.Deserialize<PaymentSucceeded>(json)!;
                    var handler = scope.ServiceProvider.GetRequiredService<PaymentSucceededHandler>();
                    await handler.HandleAsync(evt);
                    break;
                }
                case BusTopology.PaymentFailed:
                {
                    var evt = JsonSerializer.Deserialize<PaymentFailed>(json)!;
                    var handler = scope.ServiceProvider.GetRequiredService<PaymentFailedHandler>();
                    await handler.HandleAsync(evt);
                    break;
                }
                default:
                    _logger.LogWarning("Unhandled routing key {RoutingKey}", routingKey);
                    break;
            }

            _channel!.BasicAck(ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process {RoutingKey}", routingKey);
            // requeue=false -> goes to dead-letter (or is dropped); avoids
            // hot-looping a poison message. Configure a DLX for production.
            _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        base.Dispose();
    }
}


