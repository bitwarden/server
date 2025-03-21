IF OBJECT_ID('[dbo].[OpaqueKeyExchangeCredential]') IS NULL
BEGIN
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
        CONSTRAINT [PK_OpaqueKeyExchangeCredential] PRIMARY KEY CLUSTERED ([UserId]), -- using this as the primary key ensure users only have one credential
        CONSTRAINT [FK_OpaqueKeyExchangeCredential_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
    )
END
GO

IF NOT EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_OpaqueKeyExchangeCredential_UserId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OpaqueKeyExchangeCredential_UserId]
        ON [dbo].[OpaqueKeyExchangeCredential]([UserId] ASC);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OpaqueKeyExchangeCredential_Create]
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

ALTER PROCEDURE [dbo].[User_DeleteById]
    @Id UNIQUEIDENTIFIER
WITH
    RECOMPILE
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @BatchSize INT = 100

    -- Delete ciphers
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION User_DeleteById_Ciphers

        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [UserId] = @Id

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION User_DeleteById_Ciphers
    END

    BEGIN TRANSACTION User_DeleteById
    -- Delete OpaqueKeyExchangeCredentials
    DELETE
    FROM
        [dbo].[OpaqueKeyExchangeCredential]
    WHERE
        [UserId] = @Id

    -- Delete WebAuthnCredentials
    DELETE
    FROM
        [dbo].[WebAuthnCredential]
    WHERE
        [UserId] = @Id

    -- Delete folders
    DELETE
    FROM
        [dbo].[Folder]
    WHERE
        [UserId] = @Id

    -- Delete AuthRequest, must be before Device
    DELETE
    FROM
        [dbo].[AuthRequest]
    WHERE
        [UserId] = @Id

    -- Delete devices
    DELETE
    FROM
        [dbo].[Device]
    WHERE
        [UserId] = @Id

    -- Delete collection users
    DELETE
        CU
    FROM
        [dbo].[CollectionUser] CU
        INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = CU.[OrganizationUserId]
    WHERE
        OU.[UserId] = @Id

    -- Delete group users
    DELETE
        GU
    FROM
        [dbo].[GroupUser] GU
        INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = GU.[OrganizationUserId]
    WHERE
        OU.[UserId] = @Id

    -- Delete AccessPolicy
    DELETE
        AP
    FROM
        [dbo].[AccessPolicy] AP
        INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = AP.[OrganizationUserId]
    WHERE
        [UserId] = @Id

    -- Delete organization users
    DELETE
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [UserId] = @Id

    -- Delete provider users
    DELETE
    FROM
        [dbo].[ProviderUser]
    WHERE
        [UserId] = @Id

    -- Delete SSO Users
    DELETE
    FROM
        [dbo].[SsoUser]
    WHERE
        [UserId] = @Id

    -- Delete Emergency Accesses
    DELETE
    FROM
        [dbo].[EmergencyAccess]
    WHERE
        [GrantorId] = @Id
        OR
        [GranteeId] = @Id

    -- Delete Sends
    DELETE
    FROM
        [dbo].[Send]
    WHERE
        [UserId] = @Id

    -- Delete Notification Status
    DELETE
    FROM
        [dbo].[NotificationStatus]
    WHERE
        [UserId] = @Id

    -- Delete Notification
    DELETE
    FROM
        [dbo].[Notification]
    WHERE
        [UserId] = @Id

    -- Finally, delete the user
    DELETE
    FROM
        [dbo].[User]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION User_DeleteById
END
GO
