CREATE PROCEDURE [dbo].[Cipher_Create]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Favorites NVARCHAR(MAX),
    @Folders NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
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
        [Favorites],
        [Folders],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @OrganizationId,
        @Type,
        @Data,
        @Favorites,
        @Folders,
        @CreationDate,
        @RevisionDate
    )
END