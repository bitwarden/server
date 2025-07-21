CREATE PROCEDURE [dbo].[Folder_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[FolderView]
    WHERE
        [UserId] = @UserId
END