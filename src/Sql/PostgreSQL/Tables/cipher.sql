DROP TABLE IF EXISTS cipher;

CREATE TABLE IF NOT EXISTS cipher (
    id              UUID        NOT NULL,
    user_id         UUID        NULL,
    organization_id UUID        NULL,
    type            SMALLINT    NOT NULL,
    data            TEXT        NOT NULL,
    favorites       TEXT        NULL,
    folders         TEXT        NULL,
    attachments     TEXT        NULL,
    creation_date   TIMESTAMPTZ NOT NULL,
    revision_date   TIMESTAMPTZ NOT NULL,
    CONSTRAINT pk_cipher PRIMARY KEY  (id),
    CONSTRAINT fk_cipher_organization FOREIGN KEY (organization_id) REFERENCES Organization (id),
    CONSTRAINT fk_cipher_user FOREIGN KEY (user_id) REFERENCES "user" (id)
);



CREATE INDEX ix_cipher_user_id_organization_id_include_all
    ON cipher(user_id ASC, organization_id ASC)
    INCLUDE (type, data, favorites, folders, attachments, creation_date, revision_date);



CREATE INDEX ix_cipher_organization_id
    ON cipher(organization_id ASC);

