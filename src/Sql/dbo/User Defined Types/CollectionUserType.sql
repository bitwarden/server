CREATE TYPE CollectionUserType AS TABLE (
    CollectionId uniqueidentifier NOT NULL,
    OrganizationUserId uniqueidentifier NOT NULL,
    ReadOnly bit NOT NULL,
    HidePasswords bit NOT NULL,
    Manage bit DEFAULT 0 NOT NULL
);
