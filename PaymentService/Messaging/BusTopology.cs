namespace PaymentService.Messaging;

public static class BusTopology
{
    public const string Exchange = "cinema.events";

    public const string BookingCreated    = nameof(Contracts.Events.BookingCreated);
    public const string PaymentSucceeded  = nameof(Contracts.Events.PaymentSucceeded);
    public const string PaymentFailed     = nameof(Contracts.Events.PaymentFailed);
}

