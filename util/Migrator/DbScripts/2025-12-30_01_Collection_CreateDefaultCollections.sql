-- Creates default user collections for organization users
-- Uses semaphore table to prevent duplicate default collections at database level
-- NOTE: this MUST be executed in a single transaction to obtain semaphore protection
CREATE OR ALTER PROCEDURE [dbo].[Collection_CreateDefaultCollections]
    @OrganizationId UNIQUEIDENTIFIER,
    @DefaultCollectionName VARCHAR(MAX),
    @OrganizationUserCollectionIds AS [dbo].[TwoGuidIdArray] READONLY -- OrganizationUserId, CollectionId
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @Now DATETIME2(7) = GETUTCDATE()

    -- Insert semaphore entries first to obtain the "lock"
    -- If this fails due to duplicate key, the entire transaction will be rolled back
    INSERT INTO [dbo].[DefaultCollectionSemaphore]
    (
        [OrganizationUserId],
        [CreationDate]
    )
    SELECT
        ids.[Id1], -- OrganizationUserId
        @Now
    FROM
        @OrganizationUserCollectionIds ids;

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
        ids.[Id2], -- CollectionId
        @OrganizationId,
        @DefaultCollectionName,
        @Now,
        @Now,
        1, -- CollectionType.DefaultUserCollection
        NULL,
        NULL
    FROM
        @OrganizationUserCollectionIds ids;

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
        ids.[Id2], -- CollectionId
        ids.[Id1], -- OrganizationUserId
        0, -- ReadOnly = false
        0, -- HidePasswords = false
        1  -- Manage = true
    FROM
        @OrganizationUserCollectionIds ids;
END
GO
