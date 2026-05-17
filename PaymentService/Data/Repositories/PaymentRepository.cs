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

    public async Task InsertAsync(PaymentEntity payment, CancellationToken ct = default)
    {
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);
    }
}