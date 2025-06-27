ALTER TABLE [dbo].[NotificationStatus]
DROP CONSTRAINT [FK_NotificationStatus_Notification];

ALTER TABLE [dbo].[NotificationStatus]
ADD CONSTRAINT [FK_NotificationStatus_Notification]
    FOREIGN KEY ([NotificationId]) REFERENCES [dbo].[Notification]([Id]) ON DELETE CASCADE;
