CREATE PROCEDURE [dbo].[FolderCipher_DeleteByUserId]
    @UserId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE FC
    FROM
        [dbo].[FolderCipher] FC
    INNER JOIN
        [dbo].[Folder] F ON F.[Id] = FC.[FolderId]
    WHERE
        F.[UserId] = @UserId
        AND [CipherId] = @CipherId
END