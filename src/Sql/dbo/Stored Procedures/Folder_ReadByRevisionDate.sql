CREATE PROCEDURE [dbo].[Folder_ReadByRevisionDate]
    @UserId UNIQUEIDENTIFIER,
    @SinceRevisionDate DATETIME
AS
BEGIN
    SELECT
        *
    FROM
        [dbo].[FolderView]
    WHERE
        [UserId] = @UserId
    AND [RevisionDate] >= @SinceRevisionDate
END
