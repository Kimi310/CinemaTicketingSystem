# Cinema Ticketing — distributed backend

Choreographed saga over RabbitMQ with three independent microservices
(no service references another service project directly).

## Projects

| Project           | Type            | Role                                                  |
|-------------------|-----------------|-------------------------------------------------------|
| `BookingService`  | ASP.NET Web API | Client-facing (Swagger). Reserves seats, owns tickets.|
| `PaymentService`  | Worker service  | Simulates payment processing.                         |
| `Contracts`       | Class library   | Shared event DTOs (NOT a microservice).               |
| RabbitMQ          | Broker (docker) | The "third runtime service". Connects everyone.       |

`BookingService` and `PaymentService` reference only `Contracts` — they
have **no direct dependency on each other**, all communication flows
through RabbitMQ.

## Saga (choreography)

```
Client --POST /api/bookings--> BookingService
                                  |
                                  | publish BookingCreated
                                  v
                              RabbitMQ -----> PaymentService
                                                 |
                              +------------------+------------------+
                              | publish PaymentSucceeded            | publish PaymentFailed
                              v                                     v
                          BookingService                        BookingService
                          confirm ticket                        cancel ticket
                          publish BookingConfirmed              publish BookingCancelled
                          (NotificationService - TBD)
```

## Running locally

```powershell
docker compose up -d                 # start RabbitMQ
dotnet build
dotnet run --project BookingService  # http://localhost:5xxx/swagger
dotnet run --project PaymentService  # in a second terminal
```

Then POST to `/api/bookings`:

```json
{
  "showingId": 1,
  "seatId": 42,
  "userId": 7,
  "amount": 39.90,
  "idempotencyKey": "f0c1a3b8-..."
}
```

Watch both consoles to see the saga unfold (~80% of payments succeed).

## Where the DB goes

All persistence is intentionally stubbed with `// TODO:` comments at the
exact places where work should happen — search the solution for
`TODO:` to find them. The data model from the diagram (Movie, Room,
Showing, Seat, User, Ticket with `idempotency_key` UNIQUE and `version`
for optimistic locking) maps onto:

- `BookingsController.Create` — `SELECT ... FOR UPDATE` on SEAT,
  INSERT TICKET (PENDING), idempotency check.
- `PaymentSucceededConsumer` — UPDATE TICKET -> CONFIRMED (optimistic lock).
- `PaymentFailedConsumer`   — UPDATE TICKET -> CANCELLED (compensation).
- `BookingCreatedHandler` (PaymentService) — INSERT PAYMENT keyed by
  `idempotency_key` to make at-least-once delivery safe.

