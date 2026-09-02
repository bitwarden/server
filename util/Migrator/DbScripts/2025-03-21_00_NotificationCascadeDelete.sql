BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Notification_SecurityTask')
    BEGIN
        ALTER TABLE [dbo].[Notification]
          DROP CONSTRAINT [FK_Notification_SecurityTask]
    END

    ALTER TABLE [dbo].[Notification]
        ADD CONSTRAINT [FK_Notification_SecurityTask] FOREIGN KEY ([TaskId]) REFERENCES [dbo].[SecurityTask] ([Id]) ON DELETE CASCADE
END
GO
