CREATE PROCEDURE [dbo].[Folder_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SELECT
        *
    FROM
        [dbo].[FolderView]
    WHERE
        [UserId] = @UserId
END
