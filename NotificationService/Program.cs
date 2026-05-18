using NotificationService.Handlers;
using NotificationService.Messaging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<RabbitMqConnection>();
builder.Services.AddScoped<BookingConfirmedHandler>();
builder.Services.AddScoped<BookingCancelledHandler>();
builder.Services.AddHostedService<RabbitMqSubscriberService>();

var host = builder.Build();
host.Run();

