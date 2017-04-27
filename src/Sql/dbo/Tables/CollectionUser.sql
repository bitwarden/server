CREATE TABLE [dbo].[CollectionUser] (
    [Id]                 UNIQUEIDENTIFIER NOT NULL,
    [CollectionId]       UNIQUEIDENTIFIER NOT NULL,
    [OrganizationUserId] UNIQUEIDENTIFIER NOT NULL,
    [ReadOnly]           BIT              NOT NULL,
    [CreationDate]       DATETIME2 (7)    NOT NULL,
    [RevisionDate]       DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_CollectionUser] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_CollectionUser_Collection] FOREIGN KEY ([CollectionId]) REFERENCES [dbo].[Collection] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_CollectionUser_OrganizationUser] FOREIGN KEY ([OrganizationUserId]) REFERENCES [dbo].[OrganizationUser] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_CollectionUser_CollectionId]
    ON [dbo].[CollectionUser]([CollectionId] ASC);

