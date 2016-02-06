CREATE PROCEDURE [dbo].[Site_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SELECT
        *
    FROM
        [dbo].[SiteView]
    WHERE
        [UserId] = @UserId
END
