CREATE TABLE [dbo].[CollectionUser] (
    [CollectionId]       UNIQUEIDENTIFIER NOT NULL,
    [OrganizationUserId] UNIQUEIDENTIFIER NOT NULL,
    [ReadOnly]           BIT              NOT NULL,
    [HidePasswords]      BIT              NOT NULL,
    CONSTRAINT [PK_CollectionUser] PRIMARY KEY CLUSTERED ([CollectionId] ASC, [OrganizationUserId] ASC),
    CONSTRAINT [FK_CollectionUser_Collection] FOREIGN KEY ([CollectionId]) REFERENCES [dbo].[Collection] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_CollectionUser_OrganizationUser] FOREIGN KEY ([OrganizationUserId]) REFERENCES [dbo].[OrganizationUser] ([Id])
);

