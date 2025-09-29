CREATE PROCEDURE [dbo].[UserSignatureKeyPair_UpdateForRotation]
    @UserId UNIQUEIDENTIFIER,
    @SignatureAlgorithm TINYINT,
    @SigningKey VARCHAR(MAX),
    @VerifyingKey VARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[UserSignatureKeyPair]
    SET [SignatureAlgorithm] = @SignatureAlgorithm,
        [SigningKey] = @SigningKey,
        [VerifyingKey] = @VerifyingKey,
        [RevisionDate] = @RevisionDate
    WHERE [UserId] = @UserId;
END
