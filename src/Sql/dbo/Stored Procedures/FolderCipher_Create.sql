CREATE PROCEDURE [dbo].[FolderCipher_Create]
    @FolderId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[FolderCipher]
    (
        [FolderId],
        [CipherId]
    )
    VALUES
    (
        @FolderId,
        @CipherId
    )
END