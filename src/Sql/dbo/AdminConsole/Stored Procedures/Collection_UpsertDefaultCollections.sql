-- Creates default user collections for organization users
-- Uses semaphore table to prevent duplicate default collections at database level
-- Cascade behavior: Organization -> OrganizationUser (CASCADE) -> DefaultCollectionSemaphore (CASCADE)
-- Organization FK uses NoAction to avoid competing cascade paths
CREATE PROCEDURE [dbo].[Collection_CreateDefaultCollections]
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

    BEGIN TRANSACTION;

    BEGIN TRY
        -- Insert semaphore entries first to obtain the "lock"
        -- If this fails due to duplicate key, the entire transaction will be rolled back
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

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH
END
