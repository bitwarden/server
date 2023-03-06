CREATE PROCEDURE [dbo].[Cipher_UpdateWithCollections]
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

    BEGIN TRANSACTION Cipher_UpdateWithCollections

    DECLARE @UpdateCollectionsSuccess INT
    EXEC @UpdateCollectionsSuccess = [dbo].[Cipher_UpdateCollections] @Id, @UserId, @OrganizationId, @CollectionIds

    IF @UpdateCollectionsSuccess < 0
    BEGIN
        COMMIT TRANSACTION Cipher_UpdateWithCollections
        SELECT -1 -- -1 = Failure
        RETURN
    END

    UPDATE
        [dbo].[Cipher]
    SET
        [UserId] = NULL,
        [OrganizationId] = @OrganizationId,
        [Data] = @Data,
        [Attachments] = @Attachments,
        [RevisionDate] = @RevisionDate,
        [DeletedDate] = @DeletedDate
        -- No need to update CreationDate, Favorites, Folders, or Type since that data will not change
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION Cipher_UpdateWithCollections

    IF @Attachments IS NOT NULL
    BEGIN
        EXEC [dbo].[Organization_UpdateStorage] @OrganizationId
        EXEC [dbo].[User_UpdateStorage] @UserId
    END

    EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @Id, @OrganizationId

    SELECT 0 -- 0 = Success
END