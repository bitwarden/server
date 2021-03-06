DROP TABLE IF EXISTS installation;


CREATE TABLE IF NOT EXISTS installation (
    id            UUID             NOT NULL,
    email         VARCHAR (256)    NOT NULL,
    key           VARCHAR (150)    NOT NULL,
    enabled       BIT              NOT NULL,
    creation_date TIMESTAMPTZ     NOT NULL,
    CONSTRAINT pk_installation PRIMARY KEY (id)
);

