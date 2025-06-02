CREATE PROCEDURE [dbo].[UserSigningKey_UpdateForRotation]
    @UserId UNIQUEIDENTIFIER,
    @KeyType TINYINT,
    @SigningKey VARCHAR(MAX),
    @VerifyingKey VARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    UPDATE [dbo].[UserSigningKey]
    SET [KeyType] = @KeyType,
        [SigningKey] = @SigningKey,
        [VerifyingKey] = @VerifyingKey,
        [RevisionDate] = @RevisionDate
    WHERE [UserId] = @UserId;
END
