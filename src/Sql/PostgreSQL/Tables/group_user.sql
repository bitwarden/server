DROP TABLE IF EXISTS group_user;

CREATE TABLE IF NOT EXISTS group_user (
    group_id             UUID NOT NULL,
    organization_user_id UUID NOT NULL,
    CONSTRAINT Pk_group_user PRIMARY KEY (group_id, organization_user_id),
    CONSTRAINT Fk_group_user_group FOREIGN KEY (group_id) REFERENCES "group" (id) ON DELETE CASCADE,
    CONSTRAINT Fk_group_user_organization_user FOREIGN KEY (organization_user_id) REFERENCES organization_user (id)
);



CREATE INDEX ix_group_user_organization_user_id
    ON group_user(organization_user_id ASC);



