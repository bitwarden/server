CREATE TABLE [dbo].[OpaqueKeyExchangeCredential]
(
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [CipherConfiguration] VARCHAR(MAX) NOT NULL,
    [CredentialBlob] VARCHAR(MAX) NOT NULL,
    [EncryptedPublicKey] VARCHAR(MAX) NOT NULL,
    [EncryptedPrivateKey] VARCHAR(MAX) NOT NULL,
    [EncryptedUserKey] VARCHAR(MAX) NULL,
    [CreationDate] DATETIME2 (7) NOT NULL,
    CONSTRAINT [PK_OpaqueKeyExchangeCredential] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OpaqueKeyExchangeCredential_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);

GO

CREATE NONCLUSTERED INDEX [IX_OpaqueKeyExchangeCredential_UserId]
    ON [dbo].[OpaqueKeyExchangeCredential]([UserId] ASC);
