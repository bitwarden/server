DROP TABLE IF EXISTS collection;

CREATE TABLE IF NOT EXISTS collection (
    id              UUID          NOT NULL,
    organization_id UUID          NOT NULL,
    name            TEXT          NOT NULL,
    external_id     VARCHAR (300) NULL,
    creation_date   TIMESTAMPTZ   NOT NULL,
    revision_date   TIMESTAMPTZ   NOT NULL,
    CONSTRAINT pk_collection PRIMARY KEY (id),
    CONSTRAINT fk_collection_organization FOREIGN KEY (organization_id) REFERENCES organization (id) ON DELETE CASCADE
);


CREATE  INDEX ix_collection_organization_id_include_all
    ON collection(organization_id ASC)
    INCLUDE(creation_date, name, revision_date);


