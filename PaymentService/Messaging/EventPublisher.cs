using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace PaymentService.Messaging;

public interface IEventPublisher
{
    void Publish<T>(T @event, string routingKey) where T : class;

    /// <summary>
    /// Publishes a pre-serialized JSON payload. Used by the outbox relay,
    /// which stores the payload at write time and just forwards bytes later.
    /// </summary>
    void PublishRaw(string jsonPayload, string routingKey);
}

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
        => PublishBytes(JsonSerializer.SerializeToUtf8Bytes(@event), routingKey);

    public void PublishRaw(string jsonPayload, string routingKey)
        => PublishBytes(Encoding.UTF8.GetBytes(jsonPayload), routingKey);

    private void PublishBytes(byte[] body, string routingKey)
    {
        using var channel = _connection.Connection.CreateModel();

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
