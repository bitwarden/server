CREATE PROCEDURE [dbo].[FolderCipher_Delete]
    @FolderId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[FolderCipher]
    WHERE
        [FolderId] = @FolderId
        AND [CipherId] = @CipherId
END