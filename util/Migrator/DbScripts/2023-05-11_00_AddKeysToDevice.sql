-- Add PublicKeyEncryptedSymmetricKey column to Device table
IF COL_LENGTH('[dbo].[Device]', 'PublicKeyEncryptedSymmetricKey') IS NULL
    BEGIN
        ALTER TABLE
            [dbo].[Device]
        ADD
            [PublicKeyEncryptedSymmetricKey] VARCHAR(MAX) NULL;
    END
GO

-- Add EncryptionKeyEncryptedPublicKey column to Device table
IF COL_LENGTH('[dbo].[Device]', 'EncryptionKeyEncryptedPublicKey') IS NULL
    BEGIN
        ALTER TABLE
            [dbo].[Device]
        ADD
            [EncryptionKeyEncryptedPublicKey] VARCHAR(MAX) NULL;
    END
GO

-- Add DeviceKeyEncryptedPrivateKey column to Device table
IF COL_LENGTH('[dbo].[Device]', 'DeviceKeyEncryptedPrivateKey') IS NULL
    BEGIN
        ALTER TABLE
            [dbo].[Device]
        ADD
            [DeviceKeyEncryptedPrivateKey] VARCHAR(MAX) NULL;
    END
GO

CREATE OR ALTER PROCEDURE [dbo].[Device_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @Type TINYINT,
    @Identifier NVARCHAR(50),
    @PushToken NVARCHAR(255),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @PublicKeyEncryptedSymmetricKey VARCHAR(MAX) = NULL,
    @EncryptionKeyEncryptedPublicKey VARCHAR(MAX) = NULL,
    @DeviceKeyEncryptedPrivateKey VARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Device]
    (
        [Id],
        [UserId],
        [Name],
        [Type],
        [Identifier],
        [PushToken],
        [CreationDate],
        [RevisionDate],
        [PublicKeyEncryptedSymmetricKey],
        [EncryptionKeyEncryptedPublicKey],
        [DeviceKeyEncryptedPrivateKey]
    )
    VALUES
    (
        @Id,
        @UserId,
        @Name,
        @Type,
        @Identifier,
        @PushToken,
        @CreationDate,
        @RevisionDate,
        @PublicKeyEncryptedSymmetricKey,
        @EncryptionKeyEncryptedPublicKey,
        @DeviceKeyEncryptedPrivateKey
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Device_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @Type TINYINT,
    @Identifier NVARCHAR(50),
    @PushToken NVARCHAR(255),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @PublicKeyEncryptedSymmetricKey VARCHAR(MAX) = NULL,
    @EncryptionKeyEncryptedPublicKey VARCHAR(MAX) = NULL,
    @DeviceKeyEncryptedPrivateKey VARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Device]
    SET
        [UserId] = @UserId,
        [Name] = @Name,
        [Type] = @Type,
        [Identifier] = @Identifier,
        [PushToken] = @PushToken,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [PublicKeyEncryptedSymmetricKey] = @PublicKeyEncryptedSymmetricKey,
        [EncryptionKeyEncryptedPublicKey] = @EncryptionKeyEncryptedPublicKey,
        [DeviceKeyEncryptedPrivateKey] = @DeviceKeyEncryptedPrivateKey
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[DeviceView]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[DeviceView]';
END
GO
