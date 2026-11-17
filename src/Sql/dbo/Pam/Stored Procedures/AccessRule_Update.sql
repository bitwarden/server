CREATE PROCEDURE [dbo].[AccessRule_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(256),
    @Description NVARCHAR(MAX) = NULL,
    @Conditions NVARCHAR(MAX),
    @SingleActiveLease BIT = 0,
    @DefaultLeaseDurationSeconds INT = NULL,
    @MaxLeaseDurationSeconds INT = NULL,
    @Enabled BIT = 1,
    @AllowsExtensions BIT = 0,
    @MaxExtensionDurationSeconds INT = NULL,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @LastEditedBy UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON
    SET XACT_ABORT ON

    DECLARE @Previous TABLE ([Enabled] BIT NOT NULL)
    DECLARE @CollectionIds AS [dbo].[GuidIdArray]

    BEGIN TRY
        BEGIN TRANSACTION

        UPDATE
            [dbo].[AccessRule]
        SET
            [OrganizationId] = @OrganizationId,
            [Name] = @Name,
            [Description] = @Description,
            [Conditions] = @Conditions,
            [SingleActiveLease] = @SingleActiveLease,
            [DefaultLeaseDurationSeconds] = @DefaultLeaseDurationSeconds,
            [MaxLeaseDurationSeconds] = @MaxLeaseDurationSeconds,
            [Enabled] = @Enabled,
            [AllowsExtensions] = @AllowsExtensions,
            [MaxExtensionDurationSeconds] = @MaxExtensionDurationSeconds,
            [CreationDate] = @CreationDate,
            [RevisionDate] = @RevisionDate,
            [LastEditedBy] = @LastEditedBy
        OUTPUT
            DELETED.[Enabled]
        INTO @Previous
        WHERE
            [Id] = @Id

        -- Members sync whether each collection is governed by an enabled rule, so toggling Enabled flips that state
        -- for every collection the rule governs. They are read while the rule row is still locked, so a concurrent
        -- Collection_SetAccessRuleAssociations either lands before this read or sees the new Enabled.
        IF EXISTS (SELECT 1 FROM @Previous WHERE [Enabled] <> @Enabled)
        BEGIN
            INSERT INTO @CollectionIds
            SELECT
                [Id]
            FROM
                [dbo].[Collection]
            WHERE
                [AccessRuleId] = @Id
        END

        COMMIT TRANSACTION
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
        BEGIN
            ROLLBACK TRANSACTION
        END;

        THROW
    END CATCH

    IF EXISTS (SELECT 1 FROM @CollectionIds)
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByCollectionIds] @CollectionIds, @OrganizationId
    END
END
