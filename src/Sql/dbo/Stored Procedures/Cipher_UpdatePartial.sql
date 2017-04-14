CREATE PROCEDURE [dbo].[Cipher_UpdatePartial]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @FolderId UNIQUEIDENTIFIER,
    @Favorite TINYINT
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @ExistingFolderId UNIQUEIDENTIFIER = NULL

    SELECT TOP 1 
        @ExistingFolderId = F.Id
    FROM
        [dbo].[FolderCipher] FC
    INNER JOIN
        [dbo].[Folder] F ON F.[Id] = FC.[FolderId]
    WHERE
        F.[UserId] = @UserId
        AND FC.[CipherId] = @Id

    IF @ExistingFolderId IS NOT NULL AND (@FolderId IS NULL OR @FolderId != @ExistingFolderId)
    BEGIN
        EXEC [dbo].[FolderCipher_Delete] @ExistingFolderId, @Id
    END
    
    IF @FolderId IS NOT NULL AND (@ExistingFolderId IS NULL OR @FolderId != @ExistingFolderId)
    BEGIN
        EXEC [dbo].[FolderCipher_Create] @FolderId, @Id, @UserId
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