using RabbitMQ.Client;

namespace BookingService.Messaging;

/// <summary>
/// Singleton wrapper around a single long-lived <see cref="IConnection"/>.
/// Channels (IModel) are cheap and created per-publish or per-consumer.
/// </summary>
public sealed class RabbitMqConnection : IDisposable
{
    public IConnection Connection { get; }

    public RabbitMqConnection(IConfiguration config)
    {
        var factory = new ConnectionFactory
        {
            HostName = config["RabbitMq:Host"] ?? "localhost",
            UserName = config["RabbitMq:User"] ?? "guest",
            Password = config["RabbitMq:Pass"] ?? "guest",
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true
        };

        Connection = factory.CreateConnection("booking-service");

        // Declare the shared topic exchange once on startup.
        using var ch = Connection.CreateModel();
        ch.ExchangeDeclare(
            exchange: BusTopology.Exchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);
    }

    public void Dispose() => Connection.Dispose();
}

