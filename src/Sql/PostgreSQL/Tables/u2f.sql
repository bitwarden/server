DROP TABLE IF EXISTS u2f CASCADE;

CREATE TABLE IF NOT EXISTS u2f (
    id            SERIAL        NOT NULL,
    user_id       UUID          NOT NULL,
    key_handle    VARCHAR (200) NULL,
    challenge     VARCHAR (200) NOT NULL,
    app_id        VARCHAR (50)  NOT NULL,
    version       VARCHAR (20)  NOT NULL,
    creation_date TIMESTAMPTZ   NOT NULL,
    CONSTRAINT pk_u2f PRIMARY KEY (id),
    CONSTRAINT fk_u2f_user FOREIGN KEY (user_id) REFERENCES "user" (id)
);


CREATE INDEX ix_u2f_creation_date
    ON u2f(creation_date ASC);


CREATE INDEX ix_u2f_user_id
    ON u2f(user_id ASC);

