using BookingService.Data.Entities;
using BookingService.Data.Repositories;
using BookingService.Messaging;
using Contracts.Events;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Handlers;

/// <summary>
/// Reacts to a successful payment by confirming the booking
/// (ticket -> CONFIRMED) and publishing <see cref="BookingConfirmed"/>.
/// </summary>
public class PaymentSucceededHandler
{
    private readonly IBookingRepository _repository;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<PaymentSucceededHandler> _logger;

    public PaymentSucceededHandler(
        IBookingRepository repository,
        IEventPublisher publisher,
        ILogger<PaymentSucceededHandler> logger)
    {
        _repository = repository;
        _publisher  = publisher;
        _logger     = logger;
    }

    public async Task HandleAsync(PaymentSucceeded msg)
    {
        _logger.LogInformation("Payment {PaymentId} succeeded for booking {BookingId}",
            msg.PaymentId, msg.BookingId);

        try
        {
            var ticket = await _repository.FindByIdAsync(msg.BookingId);

            if (ticket is null)
            {
                _logger.LogError("Booking {BookingId} not found — cannot confirm", msg.BookingId);
                return;
            }

            // Idempotency: already processed, do not re-publish.
            if (ticket.Status == TicketStatus.Confirmed)
            {
                _logger.LogWarning("Booking {BookingId} already confirmed, skipping", msg.BookingId);
                return;
            }

            if (ticket.Status == TicketStatus.Cancelled)
            {
                _logger.LogError("Booking {BookingId} is CANCELLED — cannot confirm after payment succeeded. Manual review required.", msg.BookingId);
                return;
            }

            await _repository.UpdateTicketStatusAsync(msg.BookingId, TicketStatus.Confirmed);

            _publisher.Publish(new BookingConfirmed(
                BookingId:      ticket.Id,
                ShowingId:      ticket.ShowingId,
                SeatId:         ticket.SeatId,
                UserId:         ticket.UserId,
                ConfirmedAtUtc: DateTime.UtcNow),
                BusTopology.BookingConfirmed);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another process updated the ticket between our read and write.
            // The subscriber will Nack and the message will be dead-lettered;
            // a retry mechanism should re-enqueue it.
            _logger.LogWarning("Concurrency conflict confirming booking {BookingId} — will retry", msg.BookingId);
            throw;
        }
    }
}