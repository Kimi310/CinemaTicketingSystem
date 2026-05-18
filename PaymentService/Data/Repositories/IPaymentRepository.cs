using PaymentService.Data.Entities;

namespace PaymentService.Data.Repositories;

public interface IPaymentRepository
{
    Task<PaymentEntity?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);

    /// <summary>
    /// Atomically inserts the payment and its outbox event in a single
    /// transaction. Either both are persisted or neither.
    /// </summary>
    Task InsertWithOutboxAsync(PaymentEntity payment, OutboxPaymentEntity outbox, CancellationToken ct = default);

    Task<IReadOnlyList<OutboxPaymentEntity>> GetUnpublishedOutboxAsync(int batchSize = 50, CancellationToken ct = default);
    Task MarkOutboxPublishedAsync(Guid id, CancellationToken ct = default);
}