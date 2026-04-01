CREATE PROCEDURE [dbo].[UserPreferences_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserPreferences]
    WHERE
        [UserId] = @UserId
END
