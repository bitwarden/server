CREATE TABLE [dbo].[PlayData] (
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [PlayId]         NVARCHAR (256)    NOT NULL,
    [UserId]         UNIQUEIDENTIFIER NULL,
    [OrganizationId] UNIQUEIDENTIFIER NULL,
    [CreationDate]   DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_PlayData] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PlayData_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PlayData_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [CK_PlayData_UserOrOrganization] CHECK (([UserId] IS NOT NULL AND [OrganizationId] IS NULL) OR ([UserId] IS NULL AND [OrganizationId] IS NOT NULL))
);

GO
CREATE NONCLUSTERED INDEX [IX_PlayData_PlayId]
    ON [dbo].[PlayData]([PlayId] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_PlayData_UserId]
    ON [dbo].[PlayData]([UserId] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_PlayData_OrganizationId]
    ON [dbo].[PlayData]([OrganizationId] ASC);
