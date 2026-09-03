-- Add the PAM access-audit log: the AccessAuditEvent store and its three stored procedures. Consolidated net-new
-- migration (the feature has not shipped), squashing the incremental steps the store went through during development.
--
-- The store is append-only and SELF-CONTAINED: AccessAuditEvent_Create snapshots the actor, requester, rule, target
-- system, and daemon display names into the row at write time, so the trail read touches no other table and a later
-- delete or rename cannot erase or rewrite history. Every snapshotted name is plaintext, so the subject cipher and
-- collection are recorded by id only and no vault data lands in the audit store. Subject and rotation ids are
-- deliberately NOT foreign keyed for the same reason: an event outlives what it references. Only OrganizationId is, so
-- the rows go when the org does. The rotation columns are NULL for non-rotation events.

IF OBJECT_ID('[dbo].[AccessAuditEvent]') IS NULL
BEGIN
    CREATE TABLE [dbo].[AccessAuditEvent] (
        [Id]                UNIQUEIDENTIFIER    NOT NULL,
        [OrganizationId]    UNIQUEIDENTIFIER    NOT NULL,
        [Kind]              TINYINT             NOT NULL,
        [Phase]             TINYINT             NOT NULL,
        [OccurredDate]      DATETIME2(7)        NOT NULL,
        [ActorId]           UNIQUEIDENTIFIER    NULL,
        [RequesterId]       UNIQUEIDENTIFIER    NULL,
        [CollectionId]      UNIQUEIDENTIFIER    NULL,
        [CipherId]          UNIQUEIDENTIFIER    NULL,
        [AccessRequestId]   UNIQUEIDENTIFIER    NULL,
        [AccessLeaseId]     UNIQUEIDENTIFIER    NULL,
        [AccessRuleId]      UNIQUEIDENTIFIER    NULL,
        [Detail]            NVARCHAR(MAX)       NULL,
        [LeaseNotBefore]    DATETIME2(7)        NULL,
        [LeaseNotAfter]     DATETIME2(7)        NULL,
        [ActorName]         NVARCHAR(50)        NULL,
        [ActorEmail]        NVARCHAR(256)       NULL,
        [RequesterName]     NVARCHAR(50)        NULL,
        [RequesterEmail]    NVARCHAR(256)       NULL,
        [RuleName]          NVARCHAR(256)       NULL,
        [CorrelationId]     UNIQUEIDENTIFIER    NOT NULL CONSTRAINT [DF_AccessAuditEvent_CorrelationId] DEFAULT NEWID(),
        [TargetSystemId]    UNIQUEIDENTIFIER    NULL,
        [TargetSystemName]  NVARCHAR(200)       NULL,
        [DaemonId]          UNIQUEIDENTIFIER    NULL,
        [DaemonName]        NVARCHAR(200)       NULL,
        [RotationConfigId]  UNIQUEIDENTIFIER    NULL,
        [RotationJobId]     UNIQUEIDENTIFIER    NULL,
        [RotationSource]    TINYINT             NULL,
        [SyncState]         TINYINT             NULL,
        CONSTRAINT [PK_AccessAuditEvent] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AccessAuditEvent_Organization] FOREIGN KEY ([OrganizationId])
            REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
    );
END
GO

-- Backfill for a table created by an earlier development cut of this store, which predates the rotation columns. A
-- freshly created table already has them, so this is a no-op.
IF COL_LENGTH('[dbo].[AccessAuditEvent]', 'TargetSystemId') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessAuditEvent] ADD
        [TargetSystemId]    UNIQUEIDENTIFIER    NULL,
        [TargetSystemName]  NVARCHAR(200)       NULL,
        [DaemonId]          UNIQUEIDENTIFIER    NULL,
        [DaemonName]        NVARCHAR(200)       NULL,
        [RotationConfigId]  UNIQUEIDENTIFIER    NULL,
        [RotationJobId]     UNIQUEIDENTIFIER    NULL,
        [RotationSource]    TINYINT             NULL,
        [SyncState]         TINYINT             NULL;
END
GO

-- Serves AccessAuditEvent_ReadPageByOrganizationId: org-scoped, ranged on OccurredDate, returned newest first a page
-- at a time. The DESC key order lets the ORDER BY come straight off the index instead of sorting the whole matched
-- range. [Id] is the third key because OccurredDate is not unique (an action's Attempt and Outcome share a timestamp)
-- and a page boundary landing among events that share an instant needs a total order to resume from.
--
-- [CorrelationId] and [Phase] are included because the before/after collapse tests every candidate row on them, not
-- just the ones the page returns, and the four subject columns because AccessAuditEvent_ReadItemsByOrganizationId
-- reads the whole range rather than a page. Both ride on this index rather than one of their own: the page read is a
-- TOP-N seek and barely notices a wider leaf row, where a second index would cost every insert.
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessAuditEvent_OrganizationId_OccurredDate_Id' AND object_id = OBJECT_ID('[dbo].[AccessAuditEvent]'))
BEGIN
    -- Rebuild for a database that ran an earlier development cut of this script, which created the index without the
    -- INCLUDE, so it ends up with the same index a fresh database gets.
    CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_OrganizationId_OccurredDate_Id]
        ON [dbo].[AccessAuditEvent] ([OrganizationId] ASC, [OccurredDate] DESC, [Id] DESC)
        INCLUDE ([CorrelationId], [Phase], [CipherId], [CollectionId], [AccessRuleId], [RuleName])
        WITH (DROP_EXISTING = ON);
END
ELSE
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_OrganizationId_OccurredDate_Id]
        ON [dbo].[AccessAuditEvent] ([OrganizationId] ASC, [OccurredDate] DESC, [Id] DESC)
        INCLUDE ([CorrelationId], [Phase], [CipherId], [CollectionId], [AccessRuleId], [RuleName]);
END
GO

-- Serves the collapse itself, which asks "is there a further-along half of this action?" once per candidate row. A
-- correlation holds one or two rows, so this is a point lookup; without it the question would be a scan.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessAuditEvent_CorrelationId' AND object_id = OBJECT_ID('[dbo].[AccessAuditEvent]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_CorrelationId]
        ON [dbo].[AccessAuditEvent] ([CorrelationId] ASC)
        INCLUDE ([OrganizationId], [OccurredDate], [Phase]);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessAuditEvent_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CorrelationId UNIQUEIDENTIFIER,
    @Kind TINYINT,
    @Phase TINYINT,
    @OccurredDate DATETIME2(7),
    @ActorId UNIQUEIDENTIFIER = NULL,
    @RequesterId UNIQUEIDENTIFIER = NULL,
    @CollectionId UNIQUEIDENTIFIER = NULL,
    @CipherId UNIQUEIDENTIFIER = NULL,
    @AccessRequestId UNIQUEIDENTIFIER = NULL,
    @AccessLeaseId UNIQUEIDENTIFIER = NULL,
    @AccessRuleId UNIQUEIDENTIFIER = NULL,
    @RuleName NVARCHAR(256) = NULL,
    @Detail NVARCHAR(MAX) = NULL,
    @LeaseNotBefore DATETIME2(7) = NULL,
    @LeaseNotAfter DATETIME2(7) = NULL,
    @TargetSystemId UNIQUEIDENTIFIER = NULL,
    @TargetSystemName NVARCHAR(200) = NULL,
    @DaemonId UNIQUEIDENTIFIER = NULL,
    @DaemonName NVARCHAR(200) = NULL,
    @RotationConfigId UNIQUEIDENTIFIER = NULL,
    @RotationJobId UNIQUEIDENTIFIER = NULL,
    @RotationSource TINYINT = NULL,
    @SyncState TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Snapshot the display names into the row at write time so the audit event is self-contained: a later delete or
    -- rename cannot change what this event says. Actor and requester names are resolved by id from [User] here and
    -- frozen, staying NULL where the id is NULL or the row is gone. The rule, target system, and daemon names come from
    -- the caller instead of a JOIN, because those entities can be deleted or renamed in the same action. The subject
    -- cipher and collection are recorded by id alone; their names are vault data, which this store never holds.
    INSERT INTO [dbo].[AccessAuditEvent]
    (
        [Id],
        [OrganizationId],
        [CorrelationId],
        [Kind],
        [Phase],
        [OccurredDate],
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
    )
    SELECT
        @Id,
        @OrganizationId,
        @CorrelationId,
        @Kind,
        @Phase,
        @OccurredDate,
        @ActorId,
        @RequesterId,
        @CollectionId,
        @CipherId,
        @AccessRequestId,
        @AccessLeaseId,
        @AccessRuleId,
        @Detail,
        @LeaseNotBefore,
        @LeaseNotAfter,
        AU.[Name],
        AU.[Email],
        RU.[Name],
        RU.[Email],
        @RuleName,
        @TargetSystemId,
        @TargetSystemName,
        @DaemonId,
        @DaemonName,
        @RotationConfigId,
        @RotationJobId,
        @RotationSource,
        @SyncState
    FROM (SELECT 1 AS [X]) Seed
    LEFT JOIN [dbo].[User] AU ON AU.[Id] = @ActorId
    LEFT JOIN [dbo].[User] RU ON RU.[Id] = @RequesterId
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
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessAuditEvent_ReadItemsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @StartDate DATETIME2(7),
    @EndDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The distinct subjects the organization's access-audit trail names between @StartDate and @EndDate: one row per
    -- cipher, one per access rule. This is what the trail's Item filter is built from, and it exists because neither
    -- of the two obvious sources works -- a page of the trail holds a page's worth of rows and cannot name every item
    -- in range, and the caller's own vault would offer every credential they hold whether the trail mentions it or not.
    --
    -- No cipher NAME is returned, because the store holds none: a cipher's name is Vault Data, so the subject cipher is
    -- recorded by id only and the caller resolves the name from its own vault, dropping the ones it cannot read.
    -- [RuleName] IS returned: plaintext organization configuration, snapshotted per event, so it travels with the id.
    --
    -- Ranked rather than aggregated so each subject carries its MOST RECENT context -- a renamed rule reads in the
    -- menu the way the newest rows read in the table, and a cipher's collection is the one it was last gated through.
    -- MIN/MAX would pick alphabetically, which for a rename is simply the wrong name.
    ;WITH [Ciphers] AS (
        SELECT
            [CipherId],
            [CollectionId],
            ROW_NUMBER() OVER (PARTITION BY [CipherId] ORDER BY [OccurredDate] DESC, [Id] DESC) AS [Rank]
        FROM [dbo].[AccessAuditEvent]
        WHERE [OrganizationId] = @OrganizationId
            AND [OccurredDate] >= @StartDate
            AND [OccurredDate] <= @EndDate
            AND [CipherId] IS NOT NULL
    ),
    [Rules] AS (
        SELECT
            [AccessRuleId],
            [RuleName],
            ROW_NUMBER() OVER (PARTITION BY [AccessRuleId] ORDER BY [OccurredDate] DESC, [Id] DESC) AS [Rank]
        FROM [dbo].[AccessAuditEvent]
        WHERE [OrganizationId] = @OrganizationId
            AND [OccurredDate] >= @StartDate
            AND [OccurredDate] <= @EndDate
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

-- Superseded by AccessAuditEvent_ReadPageByOrganizationId, which reads the same rows ranged, filtered and collapsed.
-- Dropped only after its replacement exists, and only ever from a database that ran an earlier development cut of this
-- script -- the feature has not shipped, so no deployed server has ever called it.
DROP PROCEDURE IF EXISTS [dbo].[AccessAuditEvent_ReadManyByOrganizationId]
GO
