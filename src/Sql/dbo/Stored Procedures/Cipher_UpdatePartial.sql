CREATE PROCEDURE [dbo].[Cipher_UpdatePartial]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @FolderId UNIQUEIDENTIFIER,
    @Favorite TINYINT
AS
BEGIN
    SET NOCOUNT ON

    IF @FolderId IS NULL
    BEGIN
        EXEC [dbo].[FolderCipher_DeleteByUserId] @UserId, @Id
    END
    ELSE IF (SELECT COUNT(1) FROM [dbo].[FolderCipher] WHERE [FolderId] = @FolderId AND [CipherId] = @Id) = 0
    BEGIN
        EXEC [dbo].[FolderCipher_Create] @FolderId, @Id
    END

    IF @Favorite = 0
    BEGIN
        EXEC [dbo].[Favorite_Delete] @UserId, @Id
    END
    ELSE IF (SELECT COUNT(1) FROM [dbo].[Favorite] WHERE [UserId] = @UserId AND [CipherId] = @Id) = 0
    BEGIN
        EXEC [dbo].[Favorite_Create] @UserId, @Id
    END
END