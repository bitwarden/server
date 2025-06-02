CREATE PROCEDURE [dbo].[UserSigningKey_UpdateForRotation]
    @UserId UNIQUEIDENTIFIER,
    @KeyType TINYINT,
    @VerifyingKey VARCHAR(MAX),
    @SigningKey VARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    UPDATE [dbo].[UserSigningKey]
    SET [KeyType] = @KeyType,
        [VerifyingKey] = @VerifyingKey,
        [SigningKey] = @SigningKey,
        [RevisionDate] = @RevisionDate
    WHERE [UserId] = @UserId;
END
