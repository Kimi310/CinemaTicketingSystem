using PaymentService.Handlers;
using PaymentService.Messaging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<RabbitMqConnection>();
builder.Services.AddSingleton<IEventPublisher, EventPublisher>();
builder.Services.AddScoped<BookingCreatedHandler>();
builder.Services.AddHostedService<RabbitMqSubscriberService>();

var host = builder.Build();
host.Run();
