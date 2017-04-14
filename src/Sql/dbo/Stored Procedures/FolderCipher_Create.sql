CREATE PROCEDURE [dbo].[FolderCipher_Create]
    @FolderId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[FolderCipher]
    (
        [FolderId],
        [CipherId],
        [UserId]
    )
    VALUES
    (
        @FolderId,
        @CipherId,
        @UserId
    )
END