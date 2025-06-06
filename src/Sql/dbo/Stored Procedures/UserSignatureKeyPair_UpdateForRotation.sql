CREATE PROCEDURE [dbo].[UserSignatureKeyPair_UpdateForRotation]
    @UserId UNIQUEIDENTIFIER,
    @SignatureKeyPairAlgorithm TINYINT,
    @SigningKey VARCHAR(MAX),
    @VerifyingKey VARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    UPDATE [dbo].[UserSignatureKeyPair]
    SET [SignatureKeyPairAlgorithm] = @SignatureKeyPairAlgorithm,
        [SigningKey] = @SigningKey,
        [VerifyingKey] = @VerifyingKey,
        [RevisionDate] = @RevisionDate
    WHERE [UserId] = @UserId;
END
