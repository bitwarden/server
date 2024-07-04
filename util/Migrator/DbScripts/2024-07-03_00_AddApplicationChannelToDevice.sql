-- Add ApplicationChannel column to Device table
IF COL_LENGTH('[dbo].[Device]', 'ApplicationChannel') IS NULL
    BEGIN
        ALTER TABLE
            [dbo].[Device]
        ADD
            [ApplicationChannel] TINYINT DEFAULT 0 NOT NULL; -- Default to production channel
    END
GO

-- Add ApplicationChannel column to Device_Create stored procedure
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
    @ApplicationChannel TINYINT
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
        [ApplicationChannel]
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
        @ApplicationChannel
    )
END
GO

-- Add ApplicationChannel column to Device_Update stored procedure
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
    @applicationChannel TINYINT
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
        [ApplicationChannel] = @applicationChannel
    WHERE
        [Id] = @Id
END
GO
