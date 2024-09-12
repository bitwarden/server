-- Notification

ALTER TABLE [dbo].[Notification]
    DROP CONSTRAINT [FK_Notification_Organization]
GO

ALTER TABLE [dbo].[Notification]
    DROP CONSTRAINT [FK_Notification_User]
GO

ALTER TABLE [dbo].[Notification]
    ADD CONSTRAINT [FK_Notification_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
GO

ALTER TABLE [dbo].[Notification]
    ADD CONSTRAINT [FK_Notification_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]) ON DELETE CASCADE
GO

-- NotificationStatus

ALTER TABLE [dbo].[NotificationStatus]
    DROP CONSTRAINT [FK_NotificationStatus_User]
GO

ALTER TABLE [dbo].[NotificationStatus]
    ADD CONSTRAINT [FK_NotificationStatus_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]) ON DELETE CASCADE
GO
