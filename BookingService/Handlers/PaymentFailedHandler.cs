using BookingService.Data.Entities;
using BookingService.Data.Repositories;
using BookingService.Messaging;
using Contracts.Events;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Handlers;

/// <summary>
/// Compensation step of the saga: payment failed, release the seat
/// (ticket -> CANCELLED) and publish <see cref="BookingCancelled"/>.
/// </summary>
public class PaymentFailedHandler
{
    private readonly IBookingRepository _repository;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<PaymentFailedHandler> _logger;

    public PaymentFailedHandler(
        IBookingRepository repository,
        IEventPublisher publisher,
        ILogger<PaymentFailedHandler> logger)
    {
        _repository = repository;
        _publisher  = publisher;
        _logger     = logger;
    }

    public async Task HandleAsync(PaymentFailed msg)
    {
        _logger.LogWarning("Payment failed for booking {BookingId}: {Reason}",
            msg.BookingId, msg.Reason);

        try
        {
            var ticket = await _repository.FindByIdAsync(msg.BookingId);

            if (ticket is null)
            {
                _logger.LogError("Booking {BookingId} not found — cannot cancel", msg.BookingId);
                return;
            }

            // Idempotency: already cancelled, nothing to do.
            if (ticket.Status == TicketStatus.Cancelled)
            {
                _logger.LogWarning("Booking {BookingId} already cancelled, skipping", msg.BookingId);
                return;
            }

            if (ticket.Status == TicketStatus.Confirmed)
            {
                // Payment succeeded and failed events arrived out of order,
                // or there's a bug upstream. Do not silently cancel a confirmed booking.
                _logger.LogError("Booking {BookingId} is CONFIRMED — cannot cancel. Manual review required.", msg.BookingId);
                return;
            }

            await _repository.UpdateTicketStatusAsync(msg.BookingId, TicketStatus.Cancelled);

            // Seat is now free again — the unique index on (showing_id, seat_id)
            // only blocks PENDING/CONFIRMED tickets, so a new booking can come in.

            _publisher.Publish(new BookingCancelled(
                BookingId:      ticket.Id,
                Reason:         msg.Reason,
                CancelledAtUtc: DateTime.UtcNow),
                BusTopology.BookingCancelled);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("Concurrency conflict cancelling booking {BookingId} — will retry", msg.BookingId);
            throw;
        }
    }
}