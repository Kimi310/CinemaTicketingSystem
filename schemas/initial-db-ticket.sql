-- booking-db

CREATE TABLE movie (
                       id               SERIAL          NOT NULL,
                       title            VARCHAR(255)    NOT NULL,
                       duration_minutes INT             NOT NULL,
                       PRIMARY KEY (id)
);

CREATE TABLE room (
                      id             SERIAL       NOT NULL,
                      name           VARCHAR(100) NOT NULL,
                      total_capacity INT          NOT NULL,
                      PRIMARY KEY (id)
);

CREATE TABLE seat (
                      id             SERIAL      NOT NULL,
                      room_id        INT         NOT NULL,
                      row_identifier VARCHAR(10) NOT NULL,
                      seat_number    INT         NOT NULL,
                      PRIMARY KEY (id),
                      CONSTRAINT fk_seat_room FOREIGN KEY (room_id) REFERENCES room (id)
);

CREATE TABLE showing (
                         id         SERIAL         NOT NULL,
                         movie_id   INT            NOT NULL,
                         room_id    INT            NOT NULL,
                         start_time TIMESTAMP      NOT NULL,
                         price      DECIMAL(10, 2) NOT NULL,
                         PRIMARY KEY (id),
                         CONSTRAINT fk_showing_movie FOREIGN KEY (movie_id) REFERENCES movie (id),
                         CONSTRAINT fk_showing_room  FOREIGN KEY (room_id)  REFERENCES room (id)
);

CREATE TABLE "user" (
                        id    SERIAL       NOT NULL,
                        email VARCHAR(255) NOT NULL UNIQUE,
                        PRIMARY KEY (id)
);

CREATE TABLE ticket (
                        id               UUID         NOT NULL DEFAULT gen_random_uuid(),
                        showing_id       INT          NOT NULL,
                        seat_id          INT          NOT NULL,
                        user_id          INT          NOT NULL,
                        status           VARCHAR(20)  NOT NULL DEFAULT 'PENDING'
                            CHECK (status IN ('PENDING', 'CONFIRMED', 'CANCELLED')),
                        idempotency_key  VARCHAR(255) NOT NULL,
                        created_at       TIMESTAMP    NOT NULL DEFAULT NOW(),
                        PRIMARY KEY (id),
                        CONSTRAINT uq_ticket_idempotency   UNIQUE      (idempotency_key),
                        CONSTRAINT fk_ticket_showing       FOREIGN KEY (showing_id) REFERENCES showing (id),
                        CONSTRAINT fk_ticket_seat          FOREIGN KEY (seat_id)    REFERENCES seat    (id),
                        CONSTRAINT fk_ticket_user          FOREIGN KEY (user_id)    REFERENCES "user"  (id)
);

-- Only one ACTIVE (PENDING/CONFIRMED) ticket per seat per showing.
-- CANCELLED tickets are excluded so compensation truly frees the seat.
CREATE UNIQUE INDEX uq_ticket_active_showing_seat
    ON ticket (showing_id, seat_id)
    WHERE status IN ('PENDING', 'CONFIRMED');

-- Outbox pattern
CREATE TABLE outbox_booking_created (
                                        id               UUID           NOT NULL DEFAULT gen_random_uuid(),
                                        showing_id       INT            NOT NULL,
                                        seat_id          INT            NOT NULL,
                                        user_id          INT            NOT NULL,
                                        amount           DECIMAL(10, 2) NOT NULL,
                                        idempotency_key  VARCHAR(255)   NOT NULL,
                                        created_at_utc   TIMESTAMP      NOT NULL DEFAULT NOW(),
                                        published_at_utc TIMESTAMP      NULL,
                                        PRIMARY KEY (id),
                                        CONSTRAINT uq_outbox_booking_idempotency UNIQUE (idempotency_key)
);

CREATE INDEX idx_outbox_unpublished ON outbox_booking_created (published_at_utc);

-- Outbox for cancellations emitted by internal jobs (PENDING expiry worker).
CREATE TABLE outbox_booking_cancelled (
                                          id               UUID         NOT NULL DEFAULT gen_random_uuid(),
                                          booking_id       UUID         NOT NULL,
                                          reason           VARCHAR(500) NOT NULL,
                                          cancelled_at_utc TIMESTAMP    NOT NULL,
                                          created_at_utc   TIMESTAMP    NOT NULL DEFAULT NOW(),
                                          published_at_utc TIMESTAMP    NULL,
                                          PRIMARY KEY (id)
);

CREATE INDEX idx_outbox_cancelled_unpublished ON outbox_booking_cancelled (published_at_utc);
