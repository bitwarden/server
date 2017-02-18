CREATE PROCEDURE [dbo].[Cipher_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @FolderId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Favorite BIT,
    @Data NVARCHAR(MAX),
    @Shares NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Cipher]
    SET
        [UserId] = @UserId,
        [FolderId] = @FolderId,
        [Type] = @Type,
        [Favorite] = @Favorite,
        [Data] = @Data,
        [Shares] = @Shares,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END