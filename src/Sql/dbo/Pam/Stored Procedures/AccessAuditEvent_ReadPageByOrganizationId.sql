CREATE PROCEDURE [dbo].[AccessAuditEvent_ReadPageByOrganizationId]
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

    -- Reads one page of the PAM access-audit trail for an entire organization from the append-only [AccessAuditEvent]
    -- store: the matching events between @StartDate and @EndDate, newest first, at most @PageSize of them. Fully
    -- SELF-CONTAINED, because every display name was resolved and frozen into the row at write time (see
    -- AccessAuditEvent_Create), so this read touches no other table and a later delete or rename of a referenced entity
    -- cannot erase or rewrite the event. Org-scoped: the caller is authorized by the AccessEventLogs permission at the
    -- endpoint. [Kind], [Phase], [RotationSource], and [SyncState] hold Bit.Pam.Enums.AccessAuditEventKind,
    -- AccessAuditEventPhase, PamRotationSource, and PamRotationSyncState.
    --
    -- Paging is keyset, not OFFSET. (@BeforeDate, @BeforeId) is the last row the caller already has, and the predicate
    -- seeks straight to that position in IX_AccessAuditEvent_OrganizationId_OccurredDate_Id, so every page costs the
    -- same no matter how deep it is. An OFFSET would instead re-serve rows: the store is append-only and read newest
    -- first, so each event written between two requests shifts the window down by one and pushes a row the caller has
    -- already seen onto the next page.
    --
    -- The list parameters carry JSON arrays ([1,13,30], ["<guid>", ...]); NULL means the dimension is unfiltered.
    -- OPENJSON rather than a table-valued parameter because Kind is a TINYINT, and only the GuidIdArray /
    -- TwoGuidIdArray / EmailArray user-defined types exist -- one mechanism for all three lists beats inventing a
    -- fourth type for one of them.
    SELECT TOP (@PageSize)
        [Id],
        [Kind],
        [Phase],
        [CorrelationId],
        [OccurredDate],
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
        AND E.[OccurredDate] >= @StartDate
        AND E.[OccurredDate] <= @EndDate
        -- Resume where the previous page stopped. Keyed on ([OccurredDate], [Id]) rather than [OccurredDate] alone: an
        -- action writes its before/after halves at one instant, so a boundary landing inside a group of events sharing
        -- a timestamp is ordinary here, and a date-only key would drop every row tied with it.
        AND (
            @BeforeDate IS NULL
            OR E.[OccurredDate] < @BeforeDate
            OR (E.[OccurredDate] = @BeforeDate AND E.[Id] < @BeforeId)
        )
        -- Collapse each action's before/after pair (shared CorrelationId) into one row: the Outcome when it landed,
        -- otherwise the lone Attempt -- which the caller flags as in-doubt. The collapse belongs here rather than in
        -- the caller because the caller sees one page and could not tell an Attempt whose Outcome sits on the next page
        -- from one that never landed. Scoped to the same range as the page, so the collapse is a function of what the
        -- range holds; an action straddling a bound reads as in-doubt at that edge rather than disappearing from both
        -- sides of it. The [Id] arm keeps the choice deterministic if a pair ever arrives with its phase written twice.
        AND NOT EXISTS (
            SELECT 1
            FROM [dbo].[AccessAuditEvent] P
            WHERE P.[CorrelationId] = E.[CorrelationId]
                AND P.[OrganizationId] = @OrganizationId
                AND P.[OccurredDate] >= @StartDate
                AND P.[OccurredDate] <= @EndDate
                AND (
                    P.[Phase] > E.[Phase]
                    OR (P.[Phase] = E.[Phase] AND P.[Id] < E.[Id])
                )
        )
        -- The dimensions are applied AFTER the collapse, to the row that survived it, because the two halves of one
        -- action need not agree: a refused activation writes its Attempt as LeaseActivated and its Outcome as
        -- LeaseActivationRejected, so filtering before the collapse would answer "activated" with an action that was
        -- turned down.
        AND (
            @Kinds IS NULL
            OR E.[Kind] IN (SELECT CAST([value] AS TINYINT) FROM OPENJSON(@Kinds))
        )
        -- An actor selection unions the chosen identities with the automatic bucket, which has no id of its own.
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
        -- The Item dimension is two columns, and they UNION rather than narrow: a rule-administration event names a
        -- rule and no cipher, so one selection spanning both is asking for either, not for the empty intersection.
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
    ORDER BY E.[OccurredDate] DESC, E.[Id] DESC
END
