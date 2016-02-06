CREATE PROCEDURE [dbo].[Cipher_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SELECT
        *
    FROM
        [dbo].[FolderView]
    WHERE
        [UserId] = @UserId

    SELECT
        *
    FROM
        [dbo].[SiteView]
    WHERE
        [UserId] = @UserId
END
