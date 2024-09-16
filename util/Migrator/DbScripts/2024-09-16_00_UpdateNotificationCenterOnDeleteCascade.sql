-- Notification

IF OBJECT_ID('[dbo].[FK_Notification_Organization]', 'F') IS NOT NULL
    BEGIN
        ALTER TABLE [dbo].[Notification]
            DROP CONSTRAINT [FK_Notification_Organization]
    END
GO

ALTER TABLE [dbo].[Notification]
    ADD CONSTRAINT [FK_Notification_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
GO

IF OBJECT_ID('[dbo].[FK_Notification_User]', 'F') IS NOT NULL
    BEGIN
        ALTER TABLE [dbo].[Notification]
            DROP CONSTRAINT [FK_Notification_User]
    END
GO

ALTER TABLE [dbo].[Notification]
    ADD CONSTRAINT [FK_Notification_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]) ON DELETE CASCADE
GO

-- NotificationStatus

IF OBJECT_ID('[dbo].[FK_NotificationStatus_Notification]', 'F') IS NOT NULL
    BEGIN
        ALTER TABLE [dbo].[NotificationStatus]
            DROP CONSTRAINT [FK_NotificationStatus_Notification]
    END
GO

ALTER TABLE [dbo].[NotificationStatus]
    ADD CONSTRAINT [FK_NotificationStatus_Notification] FOREIGN KEY ([NotificationId]) REFERENCES [dbo].[Notification] ([Id]) ON DELETE CASCADE
GO

IF NOT EXISTS(SELECT name
              FROM sys.indexes
              WHERE name = 'IX_NotificationStatus_UserId')
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_NotificationStatus_UserId]
            ON [dbo].[NotificationStatus] ([UserId] ASC);
    END
GO
