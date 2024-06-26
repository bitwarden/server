CREATE TABLE [dbo].[WebAuthnCredential] (
    [Id]                    UNIQUEIDENTIFIER NOT NULL,
    [UserId]                UNIQUEIDENTIFIER NOT NULL,
    [Name]                  NVARCHAR (50)    NOT NULL,
    [PublicKey]             VARCHAR (256)    NOT NULL,
    [CredentialId]          VARCHAR (256)    NOT NULL,
    [Counter]               INT              NOT NULL,
    [Type]                  VARCHAR (20)     NULL,
    [AaGuid]                UNIQUEIDENTIFIER NOT NULL,
    [EncryptedUserKey]      VARCHAR (MAX)    NULL,
    [EncryptedPrivateKey]   VARCHAR (MAX)    NULL,
    [EncryptedPublicKey]    VARCHAR (MAX)    NULL,
    [SupportsPrf]           BIT              NOT NULL,
    [CreationDate]          DATETIME2 (7)    NOT NULL,
    [RevisionDate]          DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_WebAuthnCredential] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_WebAuthnCredential_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_WebAuthnCredential_UserId]
    ON [dbo].[WebAuthnCredential]([UserId] ASC);

