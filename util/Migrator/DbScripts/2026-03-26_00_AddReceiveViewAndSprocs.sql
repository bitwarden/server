CREATE OR ALTER VIEW [dbo].[ReceiveView]
AS
SELECT
    *
FROM
    [dbo].[Receive]
GO

CREATE OR ALTER PROCEDURE [dbo].[Receive_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
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

CREATE OR ALTER PROCEDURE [dbo].[Receive_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserId UNIQUEIDENTIFIER

    SELECT TOP 1
        @UserId = [UserId]
    FROM
        [dbo].[Receive]
    WHERE
        [Id] = @Id

    DELETE
    FROM
        [dbo].[Receive]
    WHERE
        [Id] = @Id

    IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Receive_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ReceiveView]
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Receive_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ReceiveView]
    WHERE
        [UserId] = @UserId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Receive_ReadByExpirationDateBefore]
    @ExpirationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ReceiveView]
    WHERE
        [ExpirationDate] < @ExpirationDate
END
GO
