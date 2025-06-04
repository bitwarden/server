CREATE PROCEDURE [dbo].[UserSignatureKeyPair_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SELECT *
    FROM [dbo].[UserSignatureKeyPairView]
    WHERE [UserId] = @UserId;
END
