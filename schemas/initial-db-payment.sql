-- payment-db

CREATE TABLE payment (
                         id               UUID           NOT NULL DEFAULT gen_random_uuid(),
                         booking_id       UUID           NOT NULL,           -- no FK, cross-service reference
                         status           VARCHAR(20)    NOT NULL CHECK (status IN ('SUCCEEDED', 'FAILED')),
                         amount           DECIMAL(10, 2) NULL,               -- populated on SUCCEEDED only
                         failure_reason   VARCHAR(500)   NULL,               -- populated on FAILED only
                         idempotency_key  VARCHAR(255)   NOT NULL,
                         processed_at_utc TIMESTAMP      NOT NULL,
                         PRIMARY KEY (id),
                         CONSTRAINT uq_payment_idempotency UNIQUE (idempotency_key)
);

CREATE INDEX idx_payment_booking_id ON payment (booking_id);

-- Outbox pattern (payment side) — atomic write with payment row,
-- relay polls unpublished rows and pushes to RabbitMQ.
CREATE TABLE outbox_payment (
                                id               UUID         NOT NULL DEFAULT gen_random_uuid(),
                                booking_id       UUID         NOT NULL,
                                event_type       VARCHAR(50)  NOT NULL,
                                payload          TEXT         NOT NULL,
                                created_at_utc   TIMESTAMP    NOT NULL DEFAULT NOW(),
                                published_at_utc TIMESTAMP    NULL,
                                PRIMARY KEY (id)
);

CREATE INDEX idx_outbox_payment_unpublished ON outbox_payment (published_at_utc);
