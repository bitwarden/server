DROP TABLE IF EXISTS organization_user;

CREATE TABLE IF NOT EXISTS organization_user (
    id              UUID          NOT NULL,
    organization_id UUID          NOT NULL,
    user_id         UUID          NULL,
    email           VARCHAR (256) NULL,
    key             TEXT          NULL,
    status          SMALLINT      NOT NULL,
    type            SMALLINT      NOT NULL,
    access_all      BIT           NOT NULL,
    external_id     VARCHAR (300) NULL,
    creation_date   TIMESTAMPTZ   NOT NULL,
    revision_date   TIMESTAMPTZ   NOT NULL,
    CONSTRAINT pk_organization_user PRIMARY KEY (id),
    CONSTRAINT fk_organization_user_organization FOREIGN KEY (organization_id) REFERENCES organization (id) ON DELETE CASCADE,
    CONSTRAINT fk_organization_user_user FOREIGN KEY (user_id) REFERENCES "user" (id)
);


CREATE INDEX ix_organization_user_user_id_organization_id_status
    ON organization_user(user_id ASC, organization_id ASC, Status ASC)
    INCLUDE (access_all);


CREATE INDEX ix_organization_user_organization_id
    ON organization_user(organization_id aSC);

