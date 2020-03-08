DROP TABLE IF EXISTS collection_group;

CREATE TABLE IF NOT EXISTS collection_group (
    collection_id UUID NOT NULL,
    group_id      UUID NOT NULL,
    read_only     BIT  NOT NULL,
    CONSTRAINT pk_collection_group PRIMARY KEY  (collection_id, group_id),
    CONSTRAINT fk_collection_group_collection FOREIGN KEY (collection_id) REFERENCES collection (id),
    CONSTRAINT fk_collection_group_group FOREIGN KEY (group_id) REFERENCES "group" (id) ON DELETE CASCADE
);

