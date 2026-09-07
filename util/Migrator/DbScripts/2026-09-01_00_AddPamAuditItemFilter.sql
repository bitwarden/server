-- Restores the Item filter now that the trail read is paged.
-- Adds AccessAuditEvent_ReadItemsByOrganizationId; reworks the cipher filter into a two-column Item.
-- Subject columns ride this index's INCLUDE rather than their own.
CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_OrganizationId_OccurredAt_Id]
    ON [dbo].[AccessAuditEvent] ([OrganizationId] ASC, [OccurredAt] DESC, [Id] DESC)
    INCLUDE ([CorrelationId], [Phase], [CipherId], [CollectionId], [AccessRuleId], [RuleName])
    WITH (DROP_EXISTING = ON)
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessAuditEvent_ReadItemsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @StartDate DATETIME2(7),
    @EndDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- One row per cipher/rule; CipherName is withheld (encrypted), RuleName (plaintext) is returned.
    -- Ranked, not aggregated, so each subject keeps its most recent name.
    ;WITH [Ciphers] AS (
        SELECT
            [CipherId],
            [CollectionId],
            ROW_NUMBER() OVER (PARTITION BY [CipherId] ORDER BY [OccurredAt] DESC, [Id] DESC) AS [Rank]
        FROM [dbo].[AccessAuditEvent]
        WHERE [OrganizationId] = @OrganizationId
            AND [OccurredAt] >= @StartDate
            AND [OccurredAt] <= @EndDate
            AND [CipherId] IS NOT NULL
    ),
    [Rules] AS (
        SELECT
            [AccessRuleId],
            [RuleName],
            ROW_NUMBER() OVER (PARTITION BY [AccessRuleId] ORDER BY [OccurredAt] DESC, [Id] DESC) AS [Rank]
        FROM [dbo].[AccessAuditEvent]
        WHERE [OrganizationId] = @OrganizationId
            AND [OccurredAt] >= @StartDate
            AND [OccurredAt] <= @EndDate
            AND [AccessRuleId] IS NOT NULL
    )
    SELECT
        [CipherId],
        [CollectionId],
        CAST(NULL AS UNIQUEIDENTIFIER) AS [RuleId],
        CAST(NULL AS NVARCHAR(256)) AS [RuleName]
    FROM [Ciphers]
    WHERE [Rank] = 1

    UNION ALL

    SELECT
        NULL,
        NULL,
        [AccessRuleId],
        [RuleName]
    FROM [Rules]
    WHERE [Rank] = 1
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessAuditEvent_ReadPageByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @StartDate DATETIME2(7),
    @EndDate DATETIME2(7),
    @PageSize INT,
    @BeforeDate DATETIME2(7) = NULL,
    @BeforeId UNIQUEIDENTIFIER = NULL,
    @Kinds NVARCHAR(MAX) = NULL,
    @ActorIds NVARCHAR(MAX) = NULL,
    @IncludeAutomatedActor BIT = 0,
    @RequesterIds NVARCHAR(MAX) = NULL,
    @CipherIds NVARCHAR(MAX) = NULL,
    @RuleIds NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Pages the org's audit trail, newest first; rows are self-contained (names frozen at write).
    -- Collapses each before/after pair here, since paging breaks a caller-side collapse.
    SELECT TOP (@PageSize)
        [Id],
        [Kind],
        [Phase],
        [CorrelationId],
        [OccurredAt],
        [OrganizationId],
        [ActorId],
        [RequesterId],
        [CollectionId],
        [CipherId],
        [AccessRequestId],
        [AccessLeaseId],
        [AccessRuleId],
        [Detail],
        [LeaseNotBefore],
        [LeaseNotAfter],
        [ActorName],
        [ActorEmail],
        [RequesterName],
        [RequesterEmail],
        [CipherName],
        [CollectionName],
        [RuleName],
        [TargetSystemId],
        [TargetSystemName],
        [DaemonId],
        [DaemonName],
        [RotationConfigId],
        [RotationJobId],
        [RotationSource],
        [SyncState]
    FROM [dbo].[AccessAuditEvent] E
    WHERE E.[OrganizationId] = @OrganizationId
        AND E.[OccurredAt] >= @StartDate
        AND E.[OccurredAt] <= @EndDate
        -- Resumes on ([OccurredAt], [Id]); ties on one instant are ordinary here.
        AND (
            @BeforeDate IS NULL
            OR E.[OccurredAt] < @BeforeDate
            OR (E.[OccurredAt] = @BeforeDate AND E.[Id] < @BeforeId)
        )
        -- Collapses each CorrelationId pair to its Outcome, or an in-doubt Attempt unresolved.
        AND NOT EXISTS (
            SELECT 1
            FROM [dbo].[AccessAuditEvent] P
            WHERE P.[CorrelationId] = E.[CorrelationId]
                AND P.[OrganizationId] = @OrganizationId
                AND P.[OccurredAt] >= @StartDate
                AND P.[OccurredAt] <= @EndDate
                AND (
                    P.[Phase] > E.[Phase]
                    OR (P.[Phase] = E.[Phase] AND P.[Id] < E.[Id])
                )
        )
        -- Applied after the collapse, since an action's two halves can disagree.
        AND (
            @Kinds IS NULL
            OR E.[Kind] IN (SELECT CAST([value] AS TINYINT) FROM OPENJSON(@Kinds))
        )
        -- Unions the chosen identities with the automatic bucket (no id of its own).
        AND (
            (@ActorIds IS NULL AND @IncludeAutomatedActor = 0)
            OR (@IncludeAutomatedActor = 1 AND E.[ActorId] IS NULL)
            OR (
                @ActorIds IS NOT NULL
                AND E.[ActorId] IN (SELECT CAST([value] AS UNIQUEIDENTIFIER) FROM OPENJSON(@ActorIds))
            )
        )
        AND (
            @RequesterIds IS NULL
            OR E.[RequesterId] IN (SELECT CAST([value] AS UNIQUEIDENTIFIER) FROM OPENJSON(@RequesterIds))
        )
        -- The Item dimension is two columns since a rule-administration event has no cipher.
        AND (
            (@CipherIds IS NULL AND @RuleIds IS NULL)
            OR (
                @CipherIds IS NOT NULL
                AND E.[CipherId] IN (SELECT CAST([value] AS UNIQUEIDENTIFIER) FROM OPENJSON(@CipherIds))
            )
            OR (
                @RuleIds IS NOT NULL
                AND E.[AccessRuleId] IN (SELECT CAST([value] AS UNIQUEIDENTIFIER) FROM OPENJSON(@RuleIds))
            )
        )
    ORDER BY E.[OccurredAt] DESC, E.[Id] DESC
END
GO
