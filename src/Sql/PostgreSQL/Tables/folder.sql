DROP TABLE IF EXISTS folder;

CREATE TABLE IF NOT EXISTS folder (
    id            UUID        NOT NULL,
    user_id       UUID        NOT NULL,
    name          TEXT        NULL,
    creation_date TIMESTAMPTZ NOT NULL,
    revision_date TIMESTAMPTZ NOT NULL,
    CONSTRAINT pk_folder PRIMARY KEY (id),
    CONSTRAINT fk_folder_user FOREIGN KEY (user_id) REFERENCES "user" (id)
);



CREATE INDEX ix_folder_user_id_include_all
    ON folder(user_id ASC)
    INCLUDE (name, creation_date, revision_date);

