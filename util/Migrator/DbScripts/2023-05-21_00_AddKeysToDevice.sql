-- Add EncryptedUserKey column to Device table
IF COL_LENGTH('[dbo].[Device]', 'EncryptedUserKey') IS NULL
    BEGIN
        ALTER TABLE
            [dbo].[Device]
        ADD
            [EncryptedUserKey] VARCHAR(MAX) NULL;
    END
GO

-- Add EncryptedPublicKey column to Device table
IF COL_LENGTH('[dbo].[Device]', 'EncryptedPublicKey') IS NULL
    BEGIN
        ALTER TABLE
            [dbo].[Device]
        ADD
            [EncryptedPublicKey] VARCHAR(MAX) NULL;
    END
GO

-- Add EncryptedPrivateKey column to Device table
IF COL_LENGTH('[dbo].[Device]', 'EncryptedPrivateKey') IS NULL
    BEGIN
        ALTER TABLE
            [dbo].[Device]
        ADD
            [EncryptedPrivateKey] VARCHAR(MAX) NULL;
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
    @EncryptedUserKey VARCHAR(MAX) = NULL,
    @EncryptedPublicKey VARCHAR(MAX) = NULL,
    @EncryptedPrivateKey VARCHAR(MAX) = NULL
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
        [EncryptedUserKey],
        [EncryptedPublicKey],
        [EncryptedPrivateKey]
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
        @EncryptedUserKey,
        @EncryptedPublicKey,
        @EncryptedPrivateKey
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
    @EncryptedUserKey VARCHAR(MAX) = NULL,
    @EncryptedPublicKey VARCHAR(MAX) = NULL,
    @EncryptedPrivateKey VARCHAR(MAX) = NULL
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
        [EncryptedUserKey] = @EncryptedUserKey,
        [EncryptedPublicKey] = @EncryptedPublicKey,
        [EncryptedPrivateKey] = @EncryptedPrivateKey
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[DeviceView]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[DeviceView]';
END
GO
