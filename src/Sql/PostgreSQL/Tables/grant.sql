DROP TABLE IF EXISTS "grant";

CREATE TABLE IF NOT EXISTS "grant" (
    key             VARCHAR (200) NOT NULL,
    type            VARCHAR (50)  NULL,
    subject_id      VARCHAR (50)  NULL,
    client_id       VARCHAR (50)  NOT NULL,
    creation_date   TIMESTAMPTZ   NOT NULL,
    expiration_date TIMESTAMPTZ   NULL,
    data            TEXT          NOT NULL,
    CONSTRAINT pk_grant PRIMARY KEY (key)
);



CREATE INDEX ix_grant_subject_id_client_id_type
    ON "grant"(subject_id ASC, client_id ASC, type ASC);


CREATE INDEX ix_grant_expiration_date
    ON "grant"(expiration_date ASC);

