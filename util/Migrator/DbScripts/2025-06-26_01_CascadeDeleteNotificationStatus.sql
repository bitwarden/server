BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_NotificationStatus_Notification')
    BEGIN
      ALTER TABLE [dbo].[NotificationStatus] DROP CONSTRAINT [FK_NotificationStatus_Notification]
    END

    ALTER TABLE [dbo].[NotificationStatus]
      ADD CONSTRAINT [FK_NotificationStatus_Notification] FOREIGN KEY ([NotificationId]) REFERENCES [dbo].[Notification]([Id]) ON DELETE CASCADE
END
GO
