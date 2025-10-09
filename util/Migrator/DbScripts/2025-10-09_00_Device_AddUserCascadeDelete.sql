IF OBJECT_ID('[dbo].[FK_Device_User]', 'F') IS NOT NULL
    BEGIN
        ALTER TABLE [dbo].[Device]
            DROP CONSTRAINT [FK_Device_User]
    END
GO

ALTER TABLE [dbo].[Device]
    ADD CONSTRAINT [FK_Device_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]) ON DELETE CASCADE
GO
