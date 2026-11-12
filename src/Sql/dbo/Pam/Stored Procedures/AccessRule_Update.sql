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

    DECLARE @Previous TABLE ([Enabled] BIT NOT NULL)

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

    -- Members sync whether each collection is governed by an enabled rule, so toggling Enabled flips that state for
    -- every collection the rule governs.
    IF EXISTS (SELECT 1 FROM @Previous WHERE [Enabled] <> @Enabled)
    BEGIN
        DECLARE @CollectionIds AS [dbo].[GuidIdArray]

        INSERT INTO @CollectionIds
        SELECT
            [Id]
        FROM
            [dbo].[Collection]
        WHERE
            [AccessRuleId] = @Id

        EXEC [dbo].[User_BumpAccountRevisionDateByCollectionIds] @CollectionIds, @OrganizationId
    END
END
