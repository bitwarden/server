DROP TABLE IF EXISTS event;

CREATE TABLE IF NOT EXISTS event (
    id                     UUID        NOT NULL,
    type                   INT         NOT NULL,
    user_id                UUID        NULL,
    organization_id        UUID        NULL,
    cipher_id              UUID        NULL,
    collection_id          UUID        NULL,
    group_id               UUID        NULL,
    organization_user_id   UUID        NULL,
    acting_user_id         UUID        NULL,
    device_type            SMALLINT    NULL,
    ip_address             VARCHAR(50) NULL,
    date                   TIMESTAMPTZ NOT NULL,
    CONSTRAINT pk_event PRIMARY KEY (id)
);


CREATE INDEX ix_event_date_organization_id_user_id
    ON event(date DESC, organization_id ASC, acting_user_id ASC, cipher_id ASC);

