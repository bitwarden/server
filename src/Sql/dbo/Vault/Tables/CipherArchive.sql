CREATE TABLE [dbo].[CipherArchive]
(
    [CipherId]     UNIQUEIDENTIFIER NOT NULL,
    [UserId]       UNIQUEIDENTIFIER NOT NULL,
    [ArchivedDate] DATETIME2(7)     NOT NULL,
    CONSTRAINT [PK_CipherArchive]
        PRIMARY KEY CLUSTERED ([CipherId], [UserId])
);
GO

ALTER TABLE [dbo].[CipherArchive]
ADD CONSTRAINT [FK_CipherArchive_Cipher]
    FOREIGN KEY ([CipherId])
    REFERENCES [dbo].[Cipher]([Id])
    ON DELETE CASCADE;
GO

ALTER TABLE [dbo].[CipherArchive]
ADD CONSTRAINT [FK_CipherArchive_User]
    FOREIGN KEY ([UserId])
    REFERENCES [dbo].[User]([Id])
    ON DELETE CASCADE;
GO

CREATE NONCLUSTERED INDEX [IX_CipherArchive_UserId]
    ON [dbo].[CipherArchive]([UserId]);
GO
