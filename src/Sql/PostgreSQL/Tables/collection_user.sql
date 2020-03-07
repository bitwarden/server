DROP TABLE IF EXISTS collection_user;

CREATE TABLE IF NOT EXISTS collection_user (
    collection_id        UUID NOT NULL,
    organization_user_id UUID NOT NULL,
    read_only            BIT  NOT NULL,
    CONSTRAINT pk_collection_user PRIMARY KEY  (collection_id, organization_user_id),
    CONSTRAINT fk_collection_user_collection FOREIGN KEY (collection_id) REFERENCES collection (id) ON DELETE CASCADE,
    CONSTRAINT fk_collection_user_organization_user FOREIGN KEY (organization_user_id) REFERENCES organization_user (id)
);

