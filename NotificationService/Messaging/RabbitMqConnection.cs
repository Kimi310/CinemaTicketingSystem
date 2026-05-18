using RabbitMQ.Client;

namespace NotificationService.Messaging;

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

        Connection = factory.CreateConnection("notification-service");

        using var ch = Connection.CreateModel();
        ch.ExchangeDeclare(
            exchange: BusTopology.Exchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);
    }

    public void Dispose() => Connection.Dispose();
}

