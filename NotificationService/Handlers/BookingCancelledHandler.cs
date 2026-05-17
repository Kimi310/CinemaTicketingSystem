using Contracts.Events;

namespace NotificationService.Handlers;

/// <summary>
/// Notifies the user that the saga rolled back (payment failed or PENDING
/// ticket expired). In production this would send an apology email.
/// </summary>
public class BookingCancelledHandler
{
    private readonly ILogger<BookingCancelledHandler> _logger;

    public BookingCancelledHandler(ILogger<BookingCancelledHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(BookingCancelled msg)
    {
        _logger.LogWarning(
            "📧 Notifying user that booking {BookingId} was cancelled at {At:o} (reason: {Reason})",
            msg.BookingId, msg.CancelledAtUtc, msg.Reason);

        return Task.CompletedTask;
    }
}

