CREATE PROCEDURE [dbo].[UserSigningKeys_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SELECT *
    FROM [dbo].[UserSigningKeys]
    WHERE [UserId] = @UserId;
END
