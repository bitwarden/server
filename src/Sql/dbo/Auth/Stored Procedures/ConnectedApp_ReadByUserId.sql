CREATE PROCEDURE [dbo].[ConnectedApp_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ConnectedAppView]
    WHERE
        [UserId] = @UserId
END
