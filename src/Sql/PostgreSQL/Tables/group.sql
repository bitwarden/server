DROP TABLE IF EXISTS "group";

CREATE TABLE IF NOT EXISTS "group" (
    id              UUID          NOT NULL,
    organization_id UUID          NOT NULL,
    name            VARCHAR (100) NOT NULL,
    access_all      BIT           NOT NULL,
    external_id     VARCHAR (300) NULL,
    creation_date   TIMESTAMPTZ   NOT NULL,
    revision_date   TIMESTAMPTZ   NOT NULL,
    CONSTRAINT pk_group PRIMARY KEY (id),
    CONSTRAINT fk_group_organization FOREIGN KEY (organization_id) REFERENCES organization (id) ON DELETE CASCADE
);

