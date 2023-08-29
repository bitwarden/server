CREATE TYPE CollectionGroupType AS TABLE (
    CollectionId uniqueidentifier NOT NULL,
    GroupId uniqueidentifier NOT NULL,
    ReadOnly bit NOT NULL,
    HidePasswords bit NOT NULL,
    Manage bit DEFAULT 0 NOT NULL
);
