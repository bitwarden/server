CREATE PROCEDURE [dbo].[FolderCipher_Delete]
    @FolderId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[FolderCipher]
    WHERE
        [FolderId] = @FolderId
        AND [CipherId] = @CipherId
        AND [UserId] = @UserId
END