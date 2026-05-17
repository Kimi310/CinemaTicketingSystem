using BookingService.Data;
using BookingService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Services;

/// <summary>
/// Periodically expires PENDING tickets that have been sitting longer than
/// <see cref="PendingTtl"/>. This is the safety net for the "30 minutes to
/// complete payment" promise from the API — if PaymentService is down or
/// just slow, the seat is eventually released so other users can book it.
///
/// Each cancellation is written atomically with an outbox row, so downstream
/// services (NotificationService) are eventually informed via the relay.
/// </summary>
public class PendingTicketExpiryService : BackgroundService
{
    public static readonly TimeSpan PendingTtl = TimeSpan.FromMinutes(30);
    private readonly TimeSpan _pollInterval = TimeSpan.FromMinutes(1);
    private const int BatchSize = 100;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingTicketExpiryService> _logger;

    public PendingTicketExpiryService(
        IServiceScopeFactory scopeFactory,
        ILogger<PendingTicketExpiryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Pending-ticket expiry worker started (TTL={Ttl})", PendingTtl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pending-ticket expiry pass failed");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task ExpireOnceAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        var cutoff = DateTime.UtcNow - PendingTtl;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // SKIP LOCKED -> if another worker (or the booking endpoint) is
            // already holding a row, we just move on instead of blocking.
            var expired = await db.Tickets
                .FromSqlRaw(
                    @"SELECT * FROM ticket
                      WHERE status = 'PENDING'
                        AND created_at < {0}
                      ORDER BY created_at
                      LIMIT {1}
                      FOR UPDATE SKIP LOCKED",
                    cutoff, BatchSize)
                .ToListAsync(ct);

            if (expired.Count == 0)
            {
                await tx.RollbackAsync(ct);
                return;
            }

            var now = DateTime.UtcNow;
            foreach (var t in expired)
            {
                t.Status = TicketStatus.Cancelled;
                db.OutboxBookingCancelled.Add(new OutboxBookingCancelledEntity
                {
                    Id             = Guid.NewGuid(),
                    BookingId      = t.Id,
                    Reason         = "PENDING ticket timed out (no payment within 30 minutes)",
                    CancelledAtUtc = now,
                    CreatedAtUtc   = now,
                });
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation("Expired {Count} PENDING ticket(s) older than {Cutoff:o}",
                expired.Count, cutoff);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}

