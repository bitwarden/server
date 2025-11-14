IF OBJECT_ID(N'[dbo].[CipherArchive]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CipherArchive]
    (
        [CipherId]     UNIQUEIDENTIFIER NOT NULL,
        [UserId]       UNIQUEIDENTIFIER NOT NULL,
        [ArchivedDate] DATETIME2(7)     NOT NULL,

        CONSTRAINT [PK_CipherArchive]
            PRIMARY KEY CLUSTERED ([CipherId], [UserId])
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_CipherArchive_Cipher'
      AND parent_object_id = OBJECT_ID(N'[dbo].[CipherArchive]', N'U')
)
BEGIN
    ALTER TABLE [dbo].[CipherArchive]
    ADD CONSTRAINT [FK_CipherArchive_Cipher]
        FOREIGN KEY ([CipherId])
        REFERENCES [dbo].[Cipher]([Id])
        ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_CipherArchive_User'
      AND parent_object_id = OBJECT_ID(N'[dbo].[CipherArchive]', N'U')
)
BEGIN
    ALTER TABLE [dbo].[CipherArchive]
    ADD CONSTRAINT [FK_CipherArchive_User]
        FOREIGN KEY ([UserId])
        REFERENCES [dbo].[User]([Id])
        ON DELETE CASCADE;
END;
GO

-- Optional index for queries by user
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_CipherArchive_UserId'
      AND object_id = OBJECT_ID(N'[dbo].[CipherArchive]', N'U')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_CipherArchive_UserId]
        ON [dbo].[CipherArchive]([UserId]);
END;
GO
