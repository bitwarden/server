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

GO

CREATE OR ALTER PROCEDURE [dbo].[OpaqueKeyExchangeCredential_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @CipherConfiguration VARCHAR(MAX) NOT NULL,
    @CredentialBlob VARCHAR(MAX) NOT NULL,
    @EncryptedPublicKey VARCHAR(MAX) NOT NULL,
    @EncryptedPrivateKey VARCHAR(MAX) NOT NULL,
    @EncryptedUserKey VARCHAR(MAX) NOT NULL,
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OpaqueKeyExchangeCredential]
        (
        [Id],
        [UserId],
        [CipherConfiguration],
        [CredentialBlob],
        [EncryptedPublicKey],
        [EncryptedPrivateKey],
        [EncryptedUserKey],
        [CreationDate]
        )
    VALUES
        (
            @Id,
            @UserId,
            @CipherConfiguration,
            @CredentialBlob,
            @EncryptedPublicKey,
            @EncryptedPrivateKey,
            @EncryptedUserKey,
            @CreationDate
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OpaqueKeyExchangeCredential_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @CipherConfiguration VARCHAR(MAX),
    @CredentialBlob VARCHAR(MAX),
    @EncryptedPublicKey VARCHAR(MAX),
    @EncryptedPrivateKey VARCHAR(MAX),
    @EncryptedUserKey VARCHAR(MAX),
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
    [dbo].[OpaqueKeyExchangeCredential]
SET
        [CipherConfiguration] = @CipherConfiguration,
        [CredentialBlob] = @CredentialBlob,
        [EncryptedPublicKey] = @EncryptedPublicKey,
        [EncryptedPrivateKey] = @EncryptedPrivateKey,
        [EncryptedUserKey] = @EncryptedUserKey,
        [CreationDate] = @CreationDate
WHERE
    [Id] = @Id AND [UserId] = @UserId
END

GO

CREATE OR ALTER PROCEDURE [dbo].[OpaqueKeyExchangeCredential_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    DELETE
    FROM
        [dbo].[OpaqueKeyExchangeCredential]
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OpaqueKeyExchangeCredential_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OpaqueKeyExchangeCredential]
    WHERE
        [UserId] = @UserId
END

GO

CREATE OR ALTER PROCEDURE [dbo].[OpaqueKeyExchangeCredential_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OpaqueKeyExchangeCredential]
    WHERE
        [Id] = @Id
END

GO