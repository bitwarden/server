CREATE OR ALTER PROCEDURE [dbo].[Collection_UpsertDefaultCollections]
    @OrganizationId UNIQUEIDENTIFIER,
    @DefaultCollectionName VARCHAR(MAX),
    @OrganizationUserIdsJson NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    -- Parse JSON once into table variable with pre-generated collection IDs
    DECLARE @OrganizationUserIds TABLE
    (
        [OrganizationUserId] UNIQUEIDENTIFIER,
        [CollectionId] UNIQUEIDENTIFIER
    );

    INSERT INTO @OrganizationUserIds
    (
        [OrganizationUserId],
        [CollectionId]
    )
    SELECT
        CAST([value] AS UNIQUEIDENTIFIER),
        NEWID()
    FROM
        OPENJSON(@OrganizationUserIdsJson);

    -- Insert semaphore entries first to obtain the "lock"
    INSERT INTO [dbo].[DefaultCollectionSemaphore]
    (
        [OrganizationId],
        [OrganizationUserId],
        [CreationDate]
    )
    SELECT
        @OrganizationId,
        ou.[OrganizationUserId],
        GETUTCDATE()
    FROM
        @OrganizationUserIds ou;

    -- Insert collections for users who obtained semaphore entries
    INSERT INTO [dbo].[Collection]
    (
        [Id],
        [OrganizationId],
        [Name],
        [CreationDate],
        [RevisionDate],
        [Type],
        [ExternalId],
        [DefaultUserCollectionEmail]
    )
    SELECT
        ou.[CollectionId],
        @OrganizationId,
        @DefaultCollectionName,
        GETUTCDATE(),
        GETUTCDATE(),
        1, -- CollectionType.DefaultUserCollection
        NULL,
        NULL
    FROM
        @OrganizationUserIds ou;

    -- Insert collection user mappings
    INSERT INTO [dbo].[CollectionUser]
    (
        [CollectionId],
        [OrganizationUserId],
        [ReadOnly],
        [HidePasswords],
        [Manage]
    )
    SELECT
        ou.[CollectionId],
        ou.[OrganizationUserId],
        0, -- ReadOnly = false
        0, -- HidePasswords = false
        1  -- Manage = true
    FROM
        @OrganizationUserIds ou;
END
GO
