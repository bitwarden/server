DROP TABLE IF EXISTS device;

CREATE TABLE IF NOT EXISTS device (
    id            UUID          NOT NULL,
    user_id       UUID          NOT NULL,
    name          VARCHAR (50)  NOT NULL,
    type          SMALLINT      NOT NULL,
    identifier    VARCHAR (50)  NOT NULL,
    push_token    VARCHAR (255) NULL,
    creation_date TIMESTAMPTZ   NOT NULL,
    revision_date TIMESTAMPTZ   NOT NULL,
    CONSTRAINT pk_device PRIMARY KEY (id),
    CONSTRAINT fk_device_user FOREIGN KEY (user_id) REFERENCES "user" (id)
);



CREATE UNIQUE INDEX ux_device_user_id_identifier
    ON device(user_id ASC, identifier ASC);



CREATE INDEX ix_device_identifier
    ON device(identifier ASC);

