CREATE TABLE [dbo].[NotificationStatus]
(
    [NotificationId] UNIQUEIDENTIFIER NOT NULL,
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [ReadDate] DATETIME2 (7) NULL,
    [DeletedDate] DATETIME2 (7) NULL,
    CONSTRAINT [PK_NotificationStatus] PRIMARY KEY CLUSTERED ([NotificationId] ASC, [UserId] ASC),
    CONSTRAINT [FK_NotificationStatus_Notification] FOREIGN KEY ([NotificationId]) REFERENCES [dbo].[Notification] ([Id]),
    CONSTRAINT [FK_NotificationStatus_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);

GO
CREATE NONCLUSTERED INDEX [IX_NotificationStatus_UserId]
    ON [dbo].[NotificationStatus]([UserId] ASC);

