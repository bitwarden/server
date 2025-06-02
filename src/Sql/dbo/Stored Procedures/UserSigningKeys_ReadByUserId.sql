CREATE PROCEDURE [dbo].[UserSigningKey_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SELECT *
    FROM [dbo].[UserSigningKey]
    WHERE [UserId] = @UserId;
END
