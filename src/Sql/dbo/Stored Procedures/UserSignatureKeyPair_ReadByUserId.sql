CREATE PROCEDURE [dbo].[UserSignatureKeyPair_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SELECT *
    FROM [dbo].[UserSignatureKeyPair]
    WHERE [UserId] = @UserId;
END
