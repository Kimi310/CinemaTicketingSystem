using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using PaymentService.Data.Repositories;
using PaymentService.Handlers;
using PaymentService.Messaging;
using PaymentService.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PaymentDb")));

builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();

builder.Services.AddSingleton<RabbitMqConnection>();
builder.Services.AddSingleton<IEventPublisher, EventPublisher>();
builder.Services.AddScoped<BookingCreatedHandler>();
builder.Services.AddHostedService<RabbitMqSubscriberService>();
builder.Services.AddHostedService<OutboxPaymentRelayService>();

var host = builder.Build();
host.Run();