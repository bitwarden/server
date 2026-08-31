-- Bound the PAM access-audit trail read: one page at a time, filtered server-side, with the before/after collapse
-- moved into the store so it survives a page boundary. Adds
-- [dbo].[AccessAuditEvent_ReadPageByOrganizationId] and the two indexes it reads through.
--
-- [dbo].[AccessAuditEvent_ReadManyByOrganizationId] is deliberately left in place: it is the read an API instance
-- that has not yet rolled over still calls.

-- The trail's own read: org-scoped, ranged on [OccurredAt], newest first, one page at a time. [Id] carries the
-- ordering past a tie so a page boundary landing among events that share an instant can be resumed exactly, and the
-- two included columns are what the collapse tests each candidate row on. Named for its key columns, so adding [Id]
-- to them replaces IX_AccessAuditEvent_OrganizationId_OccurredAt rather than altering it.
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

-- Superseded by the index above, which leads on the same two columns. Dropped only once its replacement exists, so a
-- server still running the unpaged read is never left without one.
DROP INDEX IF EXISTS [IX_AccessAuditEvent_OrganizationId_OccurredAt] ON [dbo].[AccessAuditEvent]
GO

-- Serves the collapse, which asks "is there a further-along half of this action?" once per candidate row.
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

    -- Reads one page of the PAM access-audit trail for an entire organization from the append-only
    -- [AccessAuditEvent] store: the matching events between @StartDate and @EndDate, newest first, at most @PageSize
    -- of them. Fully SELF-CONTAINED -- the display names were resolved and frozen into the row at write time (see
    -- AccessAuditEvent_Create), so this read touches no other table. Org-scoped: the caller is authorized by the
    -- AccessEventLogs permission at the endpoint.
    --
    -- The list parameters carry JSON arrays ([1,13,30], ["<guid>", ...]); NULL means the dimension is unfiltered.
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
        -- Resume where the previous page stopped, keyed on ([OccurredAt], [Id]): an action writes its before/after
        -- halves at one instant, so a date-only key would drop every row tied with the boundary.
        AND (
            @BeforeDate IS NULL
            OR E.[OccurredAt] < @BeforeDate
            OR (E.[OccurredAt] = @BeforeDate AND E.[Id] < @BeforeId)
        )
        -- Collapse each action's before/after pair (shared CorrelationId) into one row: the Outcome when it landed,
        -- otherwise the lone Attempt -- which the response flags as in-doubt. Scoped to the same range as the page.
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
        -- Applied AFTER the collapse, to the row that survived it: the two halves of one action need not agree, since
        -- a refused activation writes its Attempt as LeaseActivated and its Outcome as LeaseActivationRejected.
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
