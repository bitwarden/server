CREATE PROCEDURE [dbo].[CipherDetails_Create]
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

    INSERT INTO [dbo].[Cipher]
    (
        [Id],
        [UserId],
        [OrganizationId],
        [Type],
        [Data],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        CASE WHEN @OrganizationId IS NULL THEN @UserId ELSE NULL END,
        @OrganizationId,
        @Type,
        @Data,
        @CreationDate,
        @RevisionDate
    )

    IF @FolderId IS NOT NULL
    BEGIN
        EXEC [dbo].[FolderCipher_Create] @FolderId, @Id, @UserId
    END

    IF @Favorite = 1
    BEGIN
        EXEC [dbo].[Favorite_Create] @UserId, @Id
    END
END