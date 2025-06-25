CREATE TABLE [dbo].[Notification]
(
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [Priority] TINYINT NOT NULL,
    [Global] BIT NOT NULL,
    [ClientType] TINYINT NOT NULL,
    [UserId] UNIQUEIDENTIFIER NULL,
    [OrganizationId] UNIQUEIDENTIFIER NULL,
    [Title] NVARCHAR (256) NULL,
    [Body] NVARCHAR (MAX) NULL,
    [CreationDate] DATETIME2 (7) NOT NULL,
    [RevisionDate] DATETIME2 (7) NOT NULL,
    [TaskId] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_Notification] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Notification_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
    CONSTRAINT [FK_Notification_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]),
    CONSTRAINT [FK_Notification_SecurityTask] FOREIGN KEY ([TaskId]) REFERENCES [dbo].[SecurityTask] ([Id]) ON DELETE CASCADE
);


GO
CREATE NONCLUSTERED INDEX [IX_Notification_Priority_CreationDate_ClientType_Global_UserId_OrganizationId]
    ON [dbo].[Notification]([Priority] DESC, [CreationDate] DESC, [ClientType], [Global], [UserId], [OrganizationId]);


GO
CREATE NONCLUSTERED INDEX [IX_Notification_UserId]
    ON [dbo].[Notification]([UserId] ASC) WHERE UserId IS NOT NULL;


GO
CREATE NONCLUSTERED INDEX [IX_Notification_OrganizationId]
    ON [dbo].[Notification]([OrganizationId] ASC) WHERE OrganizationId IS NOT NULL;

GO
CREATE NONCLUSTERED INDEX [IX_Notification_TaskId]
    ON [dbo].[Notification] ([TaskId] ASC) WHERE TaskId IS NOT NULL;
