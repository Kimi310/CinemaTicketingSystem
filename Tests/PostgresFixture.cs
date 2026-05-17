using BookingService.Data;
using BookingService.Data.Entities;
using BookingService.Data.Repositories;
using BookingService.Messaging;
using BookingService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace BookingService.Tests;

/// <summary>
/// Spins up a real PostgreSQL container (Testcontainers) and provisions the
/// ticket schema. Shared across all tests in <c>BookingFixtureCollection</c>
/// so the heavy container start cost is paid once per test run.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:15")
            .WithDatabase("ticket_db_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Bootstrap schema via EF Core (entity definitions are the source of truth).
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
    }

    public BookingDbContext CreateDbContext()
    {
        var opts = new DbContextOptionsBuilder<BookingDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new BookingDbContext(opts);
    }

    public BookingService.Services.BookingService CreateBookingService(BookingDbContext db)
    {
        var repo = new BookingRepository(db, NullLogger<BookingRepository>.Instance);
        return new BookingService.Services.BookingService(repo, db, NullLogger<BookingService.Services.BookingService>.Instance);
    }

    /// <summary>Wipes mutable state between tests so they stay isolated.</summary>
    public async Task ResetAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE outbox_booking_created, outbox_booking_cancelled, ticket RESTART IDENTITY CASCADE");
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public class BookingFixtureCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}

/// <summary>No-op publisher so handler/service tests don't need RabbitMQ.</summary>
public sealed class NullEventPublisher : IEventPublisher
{
    public List<(string RoutingKey, object Event)> Published { get; } = new();
    public void Publish<T>(T @event, string routingKey) where T : class
        => Published.Add((routingKey, @event));
}

