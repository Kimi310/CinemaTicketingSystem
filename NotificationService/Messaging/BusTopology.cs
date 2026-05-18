namespace NotificationService.Messaging;

/// <summary>
/// Shared bus constants. Routing keys mirror the event type names in
/// <c>Contracts.Events.*</c> so every service agrees on the topology.
/// </summary>
public static class BusTopology
{
    public const string Exchange = "cinema.events";

    public const string BookingConfirmed = nameof(Contracts.Events.BookingConfirmed);
    public const string BookingCancelled = nameof(Contracts.Events.BookingCancelled);
}

