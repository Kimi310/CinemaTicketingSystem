using BookingService.Data;
using BookingService.Data.Repositories;
using BookingService.Services;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Extensions;

public static class DataServiceExtensions
{
    public static IServiceCollection AddDataServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<BookingDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("TicketDb")));

        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IBookingService, Services.BookingService>();

        return services;
    }
}