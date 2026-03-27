IF COL_LENGTH('[dbo].[Receive]', 'Name') IS NULL
BEGIN
    ALTER TABLE [dbo].[Receive]
        ADD [Name] VARCHAR(MAX) NOT NULL;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Receive_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @Data VARCHAR(MAX),
    @UserKeyWrappedSharedContentEncryptionKey VARCHAR(MAX),
    @UserKeyWrappedPrivateKey VARCHAR(MAX),
    @ScekWrappedPublicKey VARCHAR(MAX),
    @Secret NVARCHAR(300),
    @UploadCount INT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @ExpirationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Receive]
    (
        [Id],
        [UserId],
        [Name],
        [Data],
        [UserKeyWrappedSharedContentEncryptionKey],
        [UserKeyWrappedPrivateKey],
        [ScekWrappedPublicKey],
        [Secret],
        [UploadCount],
        [CreationDate],
        [RevisionDate],
        [ExpirationDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @Name,
        @Data,
        @UserKeyWrappedSharedContentEncryptionKey,
        @UserKeyWrappedPrivateKey,
        @ScekWrappedPublicKey,
        @Secret,
        @UploadCount,
        @CreationDate,
        @RevisionDate,
        @ExpirationDate
    )

    IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO


CREATE OR ALTER PROCEDURE [dbo].[Receive_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @Data VARCHAR(MAX),
    @UserKeyWrappedSharedContentEncryptionKey VARCHAR(MAX),
    @UserKeyWrappedPrivateKey VARCHAR(MAX),
    @ScekWrappedPublicKey VARCHAR(MAX),
    @Secret NVARCHAR(300),
    @UploadCount INT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @ExpirationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Receive]
    SET
        [UserId] = @UserId,
        [Name] = @Name,
        [Data] = @Data,
        [UserKeyWrappedSharedContentEncryptionKey] = @UserKeyWrappedSharedContentEncryptionKey,
        [UserKeyWrappedPrivateKey] = @UserKeyWrappedPrivateKey,
        [ScekWrappedPublicKey] = @ScekWrappedPublicKey,
        [Secret] = @Secret,
        [UploadCount] = @UploadCount,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [ExpirationDate] = @ExpirationDate
    WHERE
        [Id] = @Id

    IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO
