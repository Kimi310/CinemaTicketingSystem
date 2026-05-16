namespace BookingService.Messaging;

/// <summary>
/// Bus-wide constants shared by every microservice (they must agree on the
/// exchange name + routing keys, since they speak directly to RabbitMQ).
/// Routing key == event type name (matches <c>Contracts.Events.*</c>).
/// </summary>
public static class BusTopology
{
    public const string Exchange = "cinema.events";

    public const string BookingCreated    = nameof(global::Contracts.Events.BookingCreated);
    public const string PaymentSucceeded  = nameof(global::Contracts.Events.PaymentSucceeded);
    public const string PaymentFailed     = nameof(global::Contracts.Events.PaymentFailed);
    public const string BookingConfirmed  = nameof(global::Contracts.Events.BookingConfirmed);
    public const string BookingCancelled  = nameof(global::Contracts.Events.BookingCancelled);
}


