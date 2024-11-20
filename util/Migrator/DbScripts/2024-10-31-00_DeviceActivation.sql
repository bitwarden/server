SET DEADLOCK_PRIORITY HIGH
GO

-- add column
IF COL_LENGTH('[dbo].[Device]', 'Active') IS NULL
    BEGIN
    ALTER TABLE
            [dbo].[Device]
        ADD
            [Active] BIT NOT NULL CONSTRAINT [DF_Device_Active] DEFAULT (1)
END
GO

-- refresh view
CREATE OR ALTER VIEW [dbo].[DeviceView]
AS
    SELECT
        *
    FROM
        [dbo].[Device]
GO

-- drop now-unused proc for deletion
IF OBJECT_ID('[dbo].[Device_DeleteById]') IS NOT NULL
    BEGIN
    DROP PROCEDURE [dbo].[Device_DeleteById]
END
GO

-- refresh procs
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
    @EncryptedPrivateKey VARCHAR(MAX) = NULL,
    @Active BIT = 1
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
        [EncryptedPrivateKey],
        [Active]
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
            @EncryptedPrivateKey,
            @Active
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
    @EncryptedPrivateKey VARCHAR(MAX) = NULL,
    @Active BIT = 1
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
        [EncryptedPrivateKey] = @EncryptedPrivateKey,
        [Active] = @Active
    WHERE
        [Id] = @Id
END
GO

SET DEADLOCK_PRIORITY NORMAL
GO
