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