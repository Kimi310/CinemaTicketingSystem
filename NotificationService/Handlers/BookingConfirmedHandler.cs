using Contracts.Events;

namespace NotificationService.Handlers;

/// <summary>
/// Final saga step: send the e-ticket to the user. The real implementation
/// would call SendGrid/SES; here we simulate by logging the delivery.
/// </summary>
public class BookingConfirmedHandler
{
    private readonly ILogger<BookingConfirmedHandler> _logger;

    public BookingConfirmedHandler(ILogger<BookingConfirmedHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(BookingConfirmed msg)
    {
        // Simulated email send. Idempotency is naturally provided by the
        // upstream BookingService only publishing BookingConfirmed once per
        // ticket (status transition PENDING -> CONFIRMED is guarded).
        _logger.LogInformation(
            "📧 Sending e-ticket to user {UserId}: booking {BookingId}, seat {SeatId} for showing {ShowingId} (confirmed at {ConfirmedAt:o})",
            msg.UserId, msg.BookingId, msg.SeatId, msg.ShowingId, msg.ConfirmedAtUtc);

        return Task.CompletedTask;
    }
}

