-- Create the idempotent stored procedure for creating default collections
-- This procedure prevents duplicate "My Items" collections for users using
-- a filtered unique constraint on (DefaultCollectionOwner, OrganizationId, Type) WHERE Type = 1.

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

    BEGIN TRANSACTION

    BEGIN TRY
        SET @WasCreated = 1

        -- Insert Collection with DefaultCollectionOwner populated for constraint enforcement
        INSERT INTO [dbo].[Collection]
        (
            [Id],
            [OrganizationId],
            [Name],
            [ExternalId],
            [CreationDate],
            [RevisionDate],
            [DefaultUserCollectionEmail],
            [Type],
            [DefaultCollectionOwner]
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
            1, -- CollectionType.DefaultUserCollection
            @OrganizationUserId
        )

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
        )

        -- Bump user account revision dates
        EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @CollectionId, @OrganizationId

        COMMIT TRANSACTION
    END TRY
    BEGIN CATCH
        -- Check if error is unique constraint violation (error 2601 or 2627)
        IF ERROR_NUMBER() IN (2601, 2627)
        BEGIN
            -- Collection already exists, return gracefully
            SET @WasCreated = 0
            IF @@TRANCOUNT > 0
                ROLLBACK TRANSACTION
        END
        ELSE
        BEGIN
            -- Unexpected error, rollback and re-throw
            IF @@TRANCOUNT > 0
                ROLLBACK TRANSACTION
            THROW
        END
    END CATCH
END
GO
