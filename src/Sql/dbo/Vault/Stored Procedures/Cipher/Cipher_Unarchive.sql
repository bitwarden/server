CREATE PROCEDURE [dbo].[Cipher_Unarchive]
    @Ids dbo.GuidIdArray READONLY,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @UtcNow DATETIME2(7) = SYSUTCDATETIME();

    DELETE Ca
    FROM [dbo].[CipherArchive] Ca
    INNER JOIN @Ids I ON Ca.CipherId = I.[Id]
    WHERE Ca.UserId = @UserId

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId

    SELECT @UtcNow;
END