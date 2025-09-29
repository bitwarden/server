CREATE TABLE [dbo].[UserSignatureKeyPair] (
    [Id]                        UNIQUEIDENTIFIER NOT NULL,
    [UserId]                    UNIQUEIDENTIFIER NOT NULL,
    [SignatureAlgorithm]        TINYINT NOT NULL,
    [SigningKey]                VARCHAR(MAX) NOT NULL,
    [VerifyingKey]              VARCHAR(MAX) NOT NULL,
    [CreationDate]              DATETIME2 (7) NOT NULL,
    [RevisionDate]              DATETIME2 (7) NOT NULL,
    CONSTRAINT [PK_UserSignatureKeyPair] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_UserSignatureKeyPair_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);
GO

ALTER TABLE [dbo].[UserSignatureKeyPair] ADD
CONSTRAINT [FK_UserSignatureKeyPair_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]) ON DELETE CASCADE;
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_UserSignatureKeyPair_UserId]
    ON [dbo].[UserSignatureKeyPair]([UserId] ASC);
GO
