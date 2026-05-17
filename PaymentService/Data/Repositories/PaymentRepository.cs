using Microsoft.EntityFrameworkCore;
using PaymentService.Data.Entities;

namespace PaymentService.Data.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _db;

    public PaymentRepository(PaymentDbContext db)
    {
        _db = db;
    }

    public Task<PaymentEntity?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
        => _db.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey, ct);

    public async Task InsertWithOutboxAsync(PaymentEntity payment, OutboxPaymentEntity outbox, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            _db.Payments.Add(payment);
            _db.OutboxPayments.Add(outbox);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public Task<IReadOnlyList<OutboxPaymentEntity>> GetUnpublishedOutboxAsync(int batchSize = 50, CancellationToken ct = default)
        => _db.OutboxPayments
            .Where(o => o.PublishedAtUtc == null)
            .OrderBy(o => o.CreatedAtUtc)
            .Take(batchSize)
            .AsNoTracking()
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<OutboxPaymentEntity>)t.Result, ct);

    public async Task MarkOutboxPublishedAsync(Guid id, CancellationToken ct = default)
    {
        await _db.OutboxPayments
            .Where(o => o.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.PublishedAtUtc, DateTime.UtcNow), ct);
    }
}