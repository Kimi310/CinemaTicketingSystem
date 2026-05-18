using BookingService.Handlers;
using BookingService.Messaging;
using BookingService.Extensions;
using BookingService.Services;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// RabbitMQ (the broker is the "third service" of the choreographed saga:
// every microservice talks ONLY to RabbitMQ, never to each other).
builder.Services.AddSingleton<RabbitMqConnection>();
builder.Services.AddSingleton<IEventPublisher, EventPublisher>();

builder.Services.AddDataServices(builder.Configuration);   
builder.Services.AddScoped<PaymentSucceededHandler>();
builder.Services.AddScoped<PaymentFailedHandler>();

// Background consumer loop.
builder.Services.AddHostedService<RabbitMqSubscriberService>();
builder.Services.AddHostedService<OutboxRelayService>();
builder.Services.AddHostedService<PendingTicketExpiryService>();


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
