-- Creates default user collections for organization users
-- Filters out existing default collections at database level
-- NOTE: this MUST be executed in a single transaction to ensure consistency
CREATE PROCEDURE [dbo].[Collection_CreateDefaultCollections]
    @OrganizationId UNIQUEIDENTIFIER,
    @DefaultCollectionName VARCHAR(MAX),
    @OrganizationUserCollectionIds AS [dbo].[TwoGuidIdArray] READONLY -- OrganizationUserId, CollectionId
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @Now DATETIME2(7) = GETUTCDATE()

    -- Filter to only users who don't have default collections
    SELECT ids.Id1, ids.Id2
    INTO #FilteredIds
    FROM @OrganizationUserCollectionIds ids
    WHERE NOT EXISTS (
        SELECT 1
        FROM [dbo].[CollectionUser] cu
        INNER JOIN [dbo].[Collection] c ON c.Id = cu.CollectionId
        WHERE c.OrganizationId = @OrganizationId
          AND c.[Type] = 1 -- CollectionType.DefaultUserCollection
          AND cu.OrganizationUserId = ids.Id1
    );

    -- Insert collections only for users who don't have default collections yet
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
        ids.Id2, -- CollectionId
        @OrganizationId,
        @DefaultCollectionName,
        @Now,
        @Now,
        1, -- CollectionType.DefaultUserCollection
        NULL,
        NULL
    FROM
        #FilteredIds ids;

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
        ids.Id2, -- CollectionId
        ids.Id1, -- OrganizationUserId
        0, -- ReadOnly = false
        0, -- HidePasswords = false
        1  -- Manage = true
    FROM
        #FilteredIds ids;

    DROP TABLE #FilteredIds;
END
