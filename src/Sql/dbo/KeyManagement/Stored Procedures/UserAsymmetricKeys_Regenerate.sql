CREATE PROCEDURE [dbo].[UserAsymmetricKeys_Regenerate]
    @UserId UNIQUEIDENTIFIER,
    @PublicKey VARCHAR(MAX),
    @PrivateKey VARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @UtcNow DATETIME2(7) = GETUTCDATE();

    UPDATE [dbo].[User]
    SET [PublicKey] = @PublicKey,
        [PrivateKey] = @PrivateKey,
        [RevisionDate] = @UtcNow,
        [AccountRevisionDate] = @UtcNow
    WHERE [Id] = @UserId
END
