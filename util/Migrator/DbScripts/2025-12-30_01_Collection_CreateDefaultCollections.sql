CREATE OR ALTER PROCEDURE [dbo].[Collection_CreateDefaultCollections]
    @OrganizationId UNIQUEIDENTIFIER,
    @DefaultCollectionName VARCHAR(MAX),
    @OrganizationUserIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @Now DATETIME2(7) = GETUTCDATE()

    -- Create temporary table to allocate collection IDs to each organizationUser
    DECLARE @CollectionsToInsert TABLE
    (
        [OrganizationUserId] UNIQUEIDENTIFIER,
        [CollectionId] UNIQUEIDENTIFIER
    );

    INSERT INTO @CollectionsToInsert
    (
        [OrganizationUserId],
        [CollectionId]
    )
    SELECT
        ou.Id,
        NEWID()
    FROM
        @OrganizationUserIds ou

    BEGIN TRANSACTION;

    BEGIN TRY
        -- Insert semaphore entries first to obtain the "lock"
        -- If this fails due to duplicate key, the entire transaction will be rolled back
        INSERT INTO [dbo].[DefaultCollectionSemaphore]
        (
            [OrganizationUserId],
            [CreationDate]
        )
        SELECT
            ou.[OrganizationUserId],
            @Now
        FROM
            @CollectionsToInsert ou;

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
            @Now,
            @Now,
            1, -- CollectionType.DefaultUserCollection
            NULL,
            NULL
        FROM
            @CollectionsToInsert ou;

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
            @CollectionsToInsert ou;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH
END
GO
