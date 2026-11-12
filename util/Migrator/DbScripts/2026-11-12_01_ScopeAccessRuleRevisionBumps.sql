CREATE OR ALTER PROCEDURE [dbo].[Collection_SetAccessRuleAssociations]
    @AccessRuleId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ToAssign AS [dbo].[GuidIdArray] READONLY,
    @ToClear AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON
    -- The clear and assign passes must apply together. Without this a run-time error in the second
    -- statement aborts only that statement, leaving the transaction open for the COMMIT below to
    -- commit the clear on its own -- detaching collections that were meant to be reassigned.
    SET XACT_ABORT ON

    DECLARE @RevisionDate DATETIME2(7) = SYSUTCDATETIME()

    DECLARE @Changed TABLE (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [PreviousAccessRuleId] UNIQUEIDENTIFIER NULL,
        [AccessRuleId] UNIQUEIDENTIFIER NULL
    )

    BEGIN TRANSACTION

    UPDATE
        C
    SET
        C.[AccessRuleId] = NULL,
        C.[RevisionDate] = @RevisionDate
    OUTPUT
        INSERTED.[Id],
        DELETED.[AccessRuleId],
        INSERTED.[AccessRuleId]
    INTO @Changed
    FROM
        [dbo].[Collection] C
    INNER JOIN
        @ToClear T ON T.[Id] = C.[Id]
    WHERE
        C.[OrganizationId] = @OrganizationId
        AND C.[AccessRuleId] = @AccessRuleId

    UPDATE
        C
    SET
        C.[AccessRuleId] = @AccessRuleId,
        C.[RevisionDate] = @RevisionDate
    OUTPUT
        INSERTED.[Id],
        DELETED.[AccessRuleId],
        INSERTED.[AccessRuleId]
    INTO @Changed
    FROM
        [dbo].[Collection] C
    INNER JOIN
        @ToAssign T ON T.[Id] = C.[Id]
    WHERE
        C.[OrganizationId] = @OrganizationId
        AND (C.[AccessRuleId] IS NULL OR C.[AccessRuleId] <> @AccessRuleId)
        -- The foreign key only proves the rule exists, not that it belongs to this organization. Without
        -- this the caller could govern its own collections with another organization's rule, handing that
        -- organization control of the conditions gating access to data it cannot see.
        AND EXISTS (
            SELECT
                1
            FROM
                [dbo].[AccessRule] AR
            WHERE
                AR.[Id] = @AccessRuleId
                AND AR.[OrganizationId] = @OrganizationId
        )

    COMMIT TRANSACTION

    -- Members sync a collection's "governed by an enabled rule" state, so only collections where that state flipped
    -- need their members to resync.
    DECLARE @FlippedCollectionIds AS [dbo].[GuidIdArray]

    INSERT INTO @FlippedCollectionIds
    SELECT DISTINCT
        CH.[Id]
    FROM
        @Changed CH
    LEFT JOIN
        [dbo].[AccessRule] PAR ON PAR.[Id] = CH.[PreviousAccessRuleId]
    LEFT JOIN
        [dbo].[AccessRule] AR ON AR.[Id] = CH.[AccessRuleId]
    WHERE
        ISNULL(PAR.[Enabled], 0) <> ISNULL(AR.[Enabled], 0)

    IF EXISTS (SELECT 1 FROM @FlippedCollectionIds)
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByCollectionIds] @FlippedCollectionIds, @OrganizationId
    END
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRule_Update]
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
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRule_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrganizationId UNIQUEIDENTIFIER
    DECLARE @Enabled BIT
    DECLARE @ClearedCollectionIds AS [dbo].[GuidIdArray]

    SELECT @OrganizationId = [OrganizationId],
        @Enabled = [Enabled]
    FROM [dbo].[AccessRule]
    WHERE [Id] = @Id

    IF @OrganizationId IS NULL
    BEGIN
        -- Already gone: idempotent no-op.
        RETURN
    END

    -- Clear the collection links first: the FK Collection.AccessRuleId -> AccessRule is ON DELETE NO ACTION, so the
    -- referencing rows must be detached before the rule can be removed. A cleared collection is simply ungoverned.
    UPDATE [dbo].[Collection]
    SET [AccessRuleId] = NULL,
        [RevisionDate] = SYSUTCDATETIME()
    OUTPUT INSERTED.[Id] INTO @ClearedCollectionIds
    WHERE [AccessRuleId] = @Id

    -- Detach the requests that pinned this rule, for the same reason: FK_AccessRequest_AccessRule is ON DELETE
    -- NO ACTION, so any request that recorded this rule as its governing rule would block the delete outright. RuleId
    -- is provenance rather than authority -- the request's own window and decision log are what was actually granted,
    -- and the column is already nullable for requests that were never gated through a stored rule.
    UPDATE [dbo].[AccessRequest]
    SET [RuleId] = NULL
    WHERE [RuleId] = @Id

    DELETE FROM [dbo].[AccessRule]
    WHERE [Id] = @Id

    -- Only an enabled rule gated its collections, so only then do their members see a change.
    IF @Enabled = 1 AND EXISTS (SELECT 1 FROM @ClearedCollectionIds)
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByCollectionIds] @ClearedCollectionIds, @OrganizationId
    END
END
GO
