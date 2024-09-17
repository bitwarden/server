
IF NOT EXISTS(SELECT name
              FROM sys.indexes
              WHERE name = 'IX_NotificationStatus_UserId')
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_NotificationStatus_UserId]
            ON [dbo].[NotificationStatus] ([UserId] ASC);
    END
GO
