using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace BookingService.Messaging;

public interface IEventPublisher
{
    void Publish<T>(T @event, string routingKey) where T : class;
}

/// <summary>
/// Publishes events to the shared topic exchange as JSON, with persistent
/// delivery mode (durable messages survive broker restart).
/// </summary>
public sealed class EventPublisher : IEventPublisher
{
    private readonly RabbitMqConnection _connection;
    private readonly ILogger<EventPublisher> _logger;

    public EventPublisher(RabbitMqConnection connection, ILogger<EventPublisher> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public void Publish<T>(T @event, string routingKey) where T : class
    {
        using var channel = _connection.Connection.CreateModel();

        var body = JsonSerializer.SerializeToUtf8Bytes(@event);

        var props = channel.CreateBasicProperties();
        props.ContentType = "application/json";
        props.DeliveryMode = 2; // persistent
        props.MessageId = Guid.NewGuid().ToString();
        props.Type = routingKey;
        props.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        channel.BasicPublish(
            exchange: BusTopology.Exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: props,
            body: body);

        _logger.LogInformation("Published {RoutingKey} ({MessageId})", routingKey, props.MessageId);
    }
}

