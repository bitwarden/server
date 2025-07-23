CREATE TABLE [dbo].[CollectionUser] (
    [CollectionId]       UNIQUEIDENTIFIER NOT NULL,
    [OrganizationUserId] UNIQUEIDENTIFIER NOT NULL,
    [ReadOnly]           BIT              NOT NULL,
    [HidePasswords]      BIT              NOT NULL,
    [Manage]             BIT              NOT NULL CONSTRAINT D_CollectionUser_Manage DEFAULT (0),
    CONSTRAINT [PK_CollectionUser] PRIMARY KEY CLUSTERED ([CollectionId] ASC, [OrganizationUserId] ASC),
    CONSTRAINT [FK_CollectionUser_Collection] FOREIGN KEY ([CollectionId]) REFERENCES [dbo].[Collection] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_CollectionUser_OrganizationUser] FOREIGN KEY ([OrganizationUserId]) REFERENCES [dbo].[OrganizationUser] ([Id])
);

GO
CREATE NONCLUSTERED INDEX IX_CollectionUser_OrganizationUserId
    ON [dbo].[CollectionUser] (OrganizationUserId)
    INCLUDE (ReadOnly, HidePasswords, Manage)

GO
CREATE NONCLUSTERED INDEX IX_CollectionUser_OrganizationUserId_ReadOnly
    ON dbo.CollectionUser (OrganizationUserId, ReadOnly)
    INCLUDE (CollectionId);

GO
UPDATE STATISTICS dbo.CollectionUser IX_CollectionUser_OrganizationUserId_ReadOnly;
