CREATE PROCEDURE [dbo].[CipherDetails_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @FolderId UNIQUEIDENTIFIER,
    @Favorite BIT
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Cipher]
    SET
        [UserId] = CASE WHEN @OrganizationId IS NULL THEN @UserId ELSE NULL END,
        [OrganizationId] = @OrganizationId,
        [Type] = @Type,
        [Data] = @Data,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id

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