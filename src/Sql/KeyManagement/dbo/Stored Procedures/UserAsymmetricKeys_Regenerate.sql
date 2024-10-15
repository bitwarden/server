CREATE PROCEDURE [dbo].[UserAsymmetricKeys_Regenerate]
    @UserId UNIQUEIDENTIFIER,
    @PublicKey VARCHAR(MAX),
    @PrivateKey VARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE [dbo].[User]
    SET [PublicKey] = @PublicKey,
        [PrivateKey] = @PrivateKey
    WHERE [Id] = @UserId
END
