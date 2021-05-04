CREATE PROCEDURE [dbo].[Cipher_CreateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Favorites NVARCHAR(MAX),
    @Folders NVARCHAR(MAX),
    @Attachments NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @DeletedDate DATETIME2(7),
    @Reprompt TINYINT,
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Cipher_Create] @Id, @UserId, @OrganizationId, @Type, @Data, @Favorites, @Folders,
        @Attachments, @CreationDate, @RevisionDate, @DeletedDate, @Reprompt

    DECLARE @UpdateCollectionsSuccess INT
    EXEC @UpdateCollectionsSuccess = [dbo].[Cipher_UpdateCollections] @Id, @UserId, @OrganizationId, @CollectionIds
END