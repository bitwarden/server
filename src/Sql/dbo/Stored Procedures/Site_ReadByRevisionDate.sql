CREATE PROCEDURE [dbo].[Site_ReadByRevisionDate]
    @UserId UNIQUEIDENTIFIER,
    @SinceRevisionDate DATETIME
AS
BEGIN
    SELECT
        *
    FROM
        [dbo].[SiteView]
    WHERE
        [UserId] = @UserId
    AND [RevisionDate] >= @SinceRevisionDate
END
