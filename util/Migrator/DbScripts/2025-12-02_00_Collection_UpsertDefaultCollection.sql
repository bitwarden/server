-- This procedure prevents duplicate "My Items" collections for users by checking
-- if a default collection already exists before attempting to create one.

CREATE OR ALTER PROCEDURE [dbo].[Collection_UpsertDefaultCollection]
    @CollectionId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @OrganizationUserId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @WasCreated BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON

    -- explicit transaction but does not change the isolation level
    BEGIN TRANSACTION;

    BEGIN TRY
        DECLARE @ExistingCollectionId UNIQUEIDENTIFIER;

        -- Check if this organization user already has a default collection
        -- SERIALIZABLE ensures range locks prevent concurrent insertions
        SELECT @ExistingCollectionId = c.Id
        FROM [dbo].[Collection] c
                 INNER JOIN [dbo].[CollectionUser] cu ON cu.CollectionId = c.Id
        WHERE cu.OrganizationUserId = @OrganizationUserId
          AND c.OrganizationId = @OrganizationId
          AND c.Type = 1; -- CollectionType.DefaultUserCollection

        -- If collection already exists, return early
        IF @ExistingCollectionId IS NOT NULL
            BEGIN
                SET @WasCreated = 0;
                COMMIT TRANSACTION;
                RETURN;
            END

        -- Create new default collection
        SET @WasCreated = 1;

        -- Insert Collection
        INSERT INTO [dbo].[Collection]
        (
            [Id],
            [OrganizationId],
            [Name],
            [ExternalId],
            [CreationDate],
            [RevisionDate],
            [DefaultUserCollectionEmail],
            [Type]
        )
        VALUES
            (
                @CollectionId,
                @OrganizationId,
                @Name,
                NULL, -- ExternalId
                @CreationDate,
                @RevisionDate,
                NULL, -- DefaultUserCollectionEmail
                1 -- CollectionType.DefaultUserCollection
            );

        -- Insert CollectionUser
        INSERT INTO [dbo].[CollectionUser]
        (
            [CollectionId],
            [OrganizationUserId],
            [ReadOnly],
            [HidePasswords],
            [Manage]
        )
        VALUES
            (
                @CollectionId,
                @OrganizationUserId,
                0, -- ReadOnly = false
                0, -- HidePasswords = false
                1  -- Manage = true
            );

        -- Bump user account revision dates
        EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @CollectionId, @OrganizationId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO
