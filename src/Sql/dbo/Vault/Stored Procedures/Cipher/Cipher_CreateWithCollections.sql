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
    @Key VARCHAR(MAX) = NULL,
    @ArchivedDate DATETIME2(7),
    @Archives NVARCHAR(MAX) = NULL,
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Cipher_Create] @Id, @UserId, @OrganizationId, @Type, @Data, @Favorites, @Folders,
        @Attachments, @CreationDate, @RevisionDate, @DeletedDate, @Reprompt, @Key, @ArchivedDate, @Archives

    DECLARE @UpdateCollectionsSuccess INT
    EXEC @UpdateCollectionsSuccess = [dbo].[Cipher_UpdateCollections] @Id, @UserId, @OrganizationId, @CollectionIds

    -- Bump the account revision date AFTER collections are assigned.
    IF @UpdateCollectionsSuccess = 0
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @Id, @OrganizationId
    END
END
