using BookingService.Contracts;
using BookingService.Data;
using BookingService.Data.Entities;
using BookingService.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Services;

public class BookingService : IBookingService
{
    private readonly IBookingRepository _repository;
    private readonly BookingDbContext _db;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        IBookingRepository repository,
        BookingDbContext db,
        ILogger<BookingService> logger)
    {
        _repository = repository;
        _db         = db;
        _logger     = logger;
    }

    public async Task<BookingResult> CreateBookingAsync(CreateBookingRequest request, CancellationToken ct = default)
    {
        // Step 1: Idempotency check (outside the lock — cheap read)
        // If we've seen this key before, return the existing booking immediately
        var existing = await _repository.FindByIdempotencyKeyAsync(request.IdempotencyKey, ct);
        if (existing is not null)
        {
            _logger.LogInformation("Idempotent replay for key {Key}, returning existing booking {Id}",
                request.IdempotencyKey, existing.Id);

            return BookingResult.Replayed(existing.Id);
        }

        // Step 2: row-level lock, conflict check and insert
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            // SELECT FOR UPDATE on the seat row.
            // Any concurrent request for the same (showing_id, seat_id) will block here until we COMMIT or ROLLBACK
            var seatLocked = await _db.Database
                .SqlQueryRaw<int>(
                    @"SELECT 1 FROM ticket
                      WHERE showing_id = {0}
                        AND seat_id    = {1}
                        AND status IN ('PENDING', 'CONFIRMED')
                      LIMIT 1
                      FOR UPDATE",
                    request.ShowingId,
                    request.SeatId)
                .ToListAsync(ct);

            if (seatLocked.Count > 0)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogInformation("Seat {SeatId} for showing {ShowingId} is already taken",
                    request.SeatId, request.ShowingId);

                return BookingResult.Conflict();
            }

            // Step 3: Persist ticket + outbox atomically 
            var bookingId = Guid.NewGuid();

            var ticket = new TicketEntity
            {
                Id             = bookingId,
                ShowingId      = request.ShowingId,
                SeatId         = request.SeatId,
                UserId         = request.UserId,
                Status         = TicketStatus.Pending,
                IdempotencyKey = request.IdempotencyKey,
                Version        = 0,
                CreatedAt      = DateTime.UtcNow,
            };

            var outboxEvent = new OutboxBookingCreatedEntity
            {
                Id             = bookingId,
                ShowingId      = request.ShowingId,
                SeatId         = request.SeatId,
                UserId         = request.UserId,
                Amount         = request.Amount,
                IdempotencyKey = request.IdempotencyKey,
                CreatedAtUtc   = DateTime.UtcNow,
            };

            _db.Tickets.Add(ticket);
            _db.OutboxBookingCreated.Add(outboxEvent);

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Booking {BookingId} created for seat {SeatId} showing {ShowingId}",
                bookingId, request.SeatId, request.ShowingId);

            return BookingResult.Created(bookingId);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Two requests passed the FOR UPDATE check simultaneously
            // (shouldn't happen, but the unique index is a final safety net).
            await transaction.RollbackAsync(ct);
            _logger.LogWarning("Unique constraint hit for seat {SeatId} showing {ShowingId} — treated as conflict",
                request.SeatId, request.ShowingId);

            return BookingResult.Conflict();
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    // Npgsql surfaces unique violations as PostgresException with code 23505.
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("23505") == true
        || ex.InnerException?.Message.Contains("unique constraint") == true;
}