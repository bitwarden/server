CREATE PROCEDURE [dbo].[UserAsymmetricKeys_Regenerate]
    @UserId UNIQUEIDENTIFIER OUTPUT,
    @PublicKey VARCHAR(MAX),
    @PrivateKey VARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    BEGIN TRANSACTION UserAsymmetricKeys_Regenerate

    UPDATE [dbo].[User]
    SET [PublicKey] = @PublicKey,
        [PrivateKey] = @PrivateKey
    WHERE [Id] = @UserId

    COMMIT TRANSACTION UserAsymmetricKeys_Regenerate
END
