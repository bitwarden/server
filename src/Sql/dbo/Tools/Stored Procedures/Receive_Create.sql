CREATE PROCEDURE [dbo].[Receive_Create]
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
