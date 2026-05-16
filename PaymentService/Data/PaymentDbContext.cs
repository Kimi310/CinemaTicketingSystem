using Microsoft.EntityFrameworkCore;
using PaymentService.Data.Entities;

namespace PaymentService.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<PaymentEntity> Payments { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentEntity>(e =>
        {
            e.ToTable("payment");

            e.HasKey(p => p.Id);

            e.Property(p => p.Id)
                .HasColumnName("id");

            e.Property(p => p.BookingId)
                .HasColumnName("booking_id")
                .IsRequired();

            e.Property(p => p.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .IsRequired();

            e.Property(p => p.Amount)
                .HasColumnName("amount")
                .HasColumnType("decimal(10,2)");

            e.Property(p => p.FailureReason)
                .HasColumnName("failure_reason")
                .HasMaxLength(500);

            e.Property(p => p.IdempotencyKey)
                .HasColumnName("idempotency_key")
                .HasMaxLength(255)
                .IsRequired();

            e.Property(p => p.ProcessedAtUtc)
                .HasColumnName("processed_at_utc")
                .IsRequired();

            e.HasIndex(p => p.IdempotencyKey)
                .IsUnique()
                .HasDatabaseName("uq_payment_idempotency");

            e.HasIndex(p => p.BookingId)
                .HasDatabaseName("idx_payment_booking_id");
        });
    }
}