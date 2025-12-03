CREATE PROCEDURE [dbo].[UserSignatureKeyPair_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @SignatureAlgorithm TINYINT,
    @SigningKey VARCHAR(MAX),
    @VerifyingKey VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[UserSignatureKeyPair]
    (
        [Id],
        [UserId],
        [SignatureAlgorithm],
        [SigningKey],
        [VerifyingKey],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @SignatureAlgorithm,
        @SigningKey,
        @VerifyingKey,
        @CreationDate,
        @RevisionDate
    )
END
