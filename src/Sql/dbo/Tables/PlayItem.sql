CREATE TABLE [dbo].[PlayItem] (
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [PlayId]         NVARCHAR (256)    NOT NULL,
    [UserId]         UNIQUEIDENTIFIER NULL,
    [OrganizationId] UNIQUEIDENTIFIER NULL,
    [CreationDate]   DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_PlayItem] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PlayItem_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PlayItem_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [CK_PlayItem_UserOrOrganization] CHECK (([UserId] IS NOT NULL AND [OrganizationId] IS NULL) OR ([UserId] IS NULL AND [OrganizationId] IS NOT NULL))
);

GO
CREATE NONCLUSTERED INDEX [IX_PlayItem_PlayId]
    ON [dbo].[PlayItem]([PlayId] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_PlayItem_UserId]
    ON [dbo].[PlayItem]([UserId] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_PlayItem_OrganizationId]
    ON [dbo].[PlayItem]([OrganizationId] ASC);
