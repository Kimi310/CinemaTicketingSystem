using System.Text;
using System.Text.Json;
using Contracts.Events;
using NotificationService.Handlers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService.Messaging;

/// <summary>
/// Subscribes the notification service to BookingConfirmed/BookingCancelled and
/// dispatches messages to handlers. This is the third subscriber in the
/// choreographed saga (see sequence diagram: NotificationService -> Send Ticket).
/// </summary>
public sealed class RabbitMqSubscriberService : BackgroundService
{
    private const string QueueName = "notification-service.queue";

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
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

        _channel.QueueDeclare(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        _channel.QueueBind(QueueName, BusTopology.Exchange, BusTopology.BookingConfirmed);
        _channel.QueueBind(QueueName, BusTopology.Exchange, BusTopology.BookingCancelled);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnMessage;

        _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);

        _logger.LogInformation("NotificationService subscriber started on queue {Queue}", QueueName);
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
                case BusTopology.BookingConfirmed:
                {
                    var evt = JsonSerializer.Deserialize<BookingConfirmed>(json)!;
                    var handler = scope.ServiceProvider.GetRequiredService<BookingConfirmedHandler>();
                    await handler.HandleAsync(evt);
                    break;
                }
                case BusTopology.BookingCancelled:
                {
                    var evt = JsonSerializer.Deserialize<BookingCancelled>(json)!;
                    var handler = scope.ServiceProvider.GetRequiredService<BookingCancelledHandler>();
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

