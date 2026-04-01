CREATE PROCEDURE [dbo].[UserPreferences_DeleteByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[UserPreferences]
    WHERE
        [UserId] = @UserId
END
