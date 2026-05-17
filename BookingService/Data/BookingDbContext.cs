using BookingService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingService.Data;

public class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options) { }
 
    public DbSet<TicketEntity> Tickets { get; set; } = default!;
    public DbSet<OutboxBookingCreatedEntity> OutboxBookingCreated { get; set; } = default!;
    public DbSet<OutboxBookingCancelledEntity> OutboxBookingCancelled { get; set; } = default!;
 
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Ticket 
        modelBuilder.Entity<TicketEntity>(e =>
        {
            e.ToTable("ticket");
 
            e.HasKey(t => t.Id);
 
            e.Property(t => t.Id)
             .HasColumnName("id");
 
            e.Property(t => t.ShowingId)
             .HasColumnName("showing_id")
             .IsRequired();
 
            e.Property(t => t.SeatId)
             .HasColumnName("seat_id")
             .IsRequired();
 
            e.Property(t => t.UserId)
             .HasColumnName("user_id")
             .IsRequired();
 
            e.Property(t => t.Status)
             .HasColumnName("status")
             .HasMaxLength(20)
             .IsRequired();
 
            e.Property(t => t.IdempotencyKey)
             .HasColumnName("idempotency_key")
             .HasMaxLength(255)
             .IsRequired();
 
            e.Property(t => t.CreatedAt)
             .HasColumnName("created_at")
             .IsRequired();
 
            // Composite unique — but only for ACTIVE tickets (PENDING/CONFIRMED).
            // A CANCELLED ticket releases the seat, so an unconditional unique
            // constraint would wrongly block re-booking after compensation.
            // Postgres partial unique index handles this cleanly.
            e.HasIndex(t => new { t.ShowingId, t.SeatId })
             .IsUnique()
             .HasFilter("status IN ('PENDING', 'CONFIRMED')")
             .HasDatabaseName("uq_ticket_active_showing_seat");
 
            e.HasIndex(t => t.IdempotencyKey)
             .IsUnique()
             .HasDatabaseName("uq_ticket_idempotency");
        });
 
        // Outbox
        modelBuilder.Entity<OutboxBookingCreatedEntity>(e =>
        {
            e.ToTable("outbox_booking_created");
 
            e.HasKey(o => o.Id);
 
            e.Property(o => o.Id)
             .HasColumnName("id");
 
            e.Property(o => o.ShowingId)
             .HasColumnName("showing_id")
             .IsRequired();
 
            e.Property(o => o.SeatId)
             .HasColumnName("seat_id")
             .IsRequired();
 
            e.Property(o => o.UserId)
             .HasColumnName("user_id")
             .IsRequired();
 
            e.Property(o => o.Amount)
             .HasColumnName("amount")
             .HasColumnType("decimal(10,2)")
             .IsRequired();
 
            e.Property(o => o.IdempotencyKey)
             .HasColumnName("idempotency_key")
             .HasMaxLength(255)
             .IsRequired();
 
            e.Property(o => o.CreatedAtUtc)
             .HasColumnName("created_at_utc")
             .IsRequired();
 
            e.Property(o => o.PublishedAtUtc)
             .HasColumnName("published_at_utc");
 
            e.HasIndex(o => o.IdempotencyKey)
             .IsUnique()
             .HasDatabaseName("uq_outbox_booking_idempotency");
 
            // Relay queries unpublished rows | partial index not supported by
            // all providers, so we use a plain index and filter in the query.
            e.HasIndex(o => o.PublishedAtUtc)
             .HasDatabaseName("idx_outbox_unpublished");
        });

        // Outbox - cancellations (from expiry worker etc.)
        modelBuilder.Entity<OutboxBookingCancelledEntity>(e =>
        {
            e.ToTable("outbox_booking_cancelled");

            e.HasKey(o => o.Id);
            e.Property(o => o.Id).HasColumnName("id");
            e.Property(o => o.BookingId).HasColumnName("booking_id").IsRequired();
            e.Property(o => o.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
            e.Property(o => o.CancelledAtUtc).HasColumnName("cancelled_at_utc").IsRequired();
            e.Property(o => o.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            e.Property(o => o.PublishedAtUtc).HasColumnName("published_at_utc");

            e.HasIndex(o => o.PublishedAtUtc).HasDatabaseName("idx_outbox_cancelled_unpublished");
        });
    }
}
