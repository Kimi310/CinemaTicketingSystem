using PaymentService.Data.Entities;

namespace PaymentService.Data.Repositories;

public interface IPaymentRepository
{
    Task<PaymentEntity?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task InsertAsync(PaymentEntity payment, CancellationToken ct = default);
}