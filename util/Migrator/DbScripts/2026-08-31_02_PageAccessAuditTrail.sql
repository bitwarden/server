-- Adds a paged, server-filtered audit-trail read (AccessAuditEvent_ReadPageByOrganizationId) and two indexes.
-- The old ReadManyByOrganizationId stays for instances not yet rolled over.
-- Ranged on [OccurredAt] newest-first, one page at a time; [Id] breaks ties.
-- Named for its key columns; adding [Id] replaces the old index, not alters it.
IF NOT EXISTS (
    SELECT 1
    FROM [sys].[indexes]
    WHERE [name] = 'IX_AccessAuditEvent_OrganizationId_OccurredAt_Id'
        AND [object_id] = OBJECT_ID('[dbo].[AccessAuditEvent]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_OrganizationId_OccurredAt_Id]
        ON [dbo].[AccessAuditEvent] ([OrganizationId] ASC, [OccurredAt] DESC, [Id] DESC)
        INCLUDE ([CorrelationId], [Phase])
END
GO

-- Superseded by the index above; dropped only after its replacement exists.
DROP INDEX IF EXISTS [IX_AccessAuditEvent_OrganizationId_OccurredAt] ON [dbo].[AccessAuditEvent]
GO

-- Serves the collapse's per-row check for a further-along half of the same action.
IF NOT EXISTS (
    SELECT 1
    FROM [sys].[indexes]
    WHERE [name] = 'IX_AccessAuditEvent_CorrelationId'
        AND [object_id] = OBJECT_ID('[dbo].[AccessAuditEvent]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_CorrelationId]
        ON [dbo].[AccessAuditEvent] ([CorrelationId] ASC)
        INCLUDE ([OrganizationId], [OccurredAt], [Phase])
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
    @CipherId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Pages the audit store between @StartDate/@EndDate, newest first; rows are self-contained.
    -- List params are JSON arrays; NULL means unfiltered.
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
        -- Resumes on ([OccurredAt], [Id]); a date-only key would drop rows tied at the boundary.
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
        AND (@CipherId IS NULL OR E.[CipherId] = @CipherId)
    ORDER BY E.[OccurredAt] DESC, E.[Id] DESC
END
GO
