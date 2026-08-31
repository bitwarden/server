-- Add the PAM access-audit log: the AccessAuditEvent store and its two stored procedures. Consolidated net-new
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
        [OccurredAt]        DATETIME2(7)        NOT NULL,
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

-- Serves AccessAuditEvent_ReadManyByOrganizationId, the only read: org-scoped, filtered on OccurredAt, returned newest
-- first a page at a time. The DESC key order lets the ORDER BY come straight off the index instead of sorting the whole
-- matched range. [Id] is the third key because OccurredAt is not unique (an action's Attempt and Outcome share a
-- timestamp) and an OFFSET page needs a total order to not double-serve or skip a boundary row.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessAuditEvent_OrganizationId_OccurredAt_Id' AND object_id = OBJECT_ID('[dbo].[AccessAuditEvent]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_OrganizationId_OccurredAt_Id]
        ON [dbo].[AccessAuditEvent] ([OrganizationId] ASC, [OccurredAt] DESC, [Id] DESC);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessAuditEvent_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CorrelationId UNIQUEIDENTIFIER,
    @Kind TINYINT,
    @Phase TINYINT,
    @OccurredAt DATETIME2(7),
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
        [OccurredAt],
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
        @OccurredAt,
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

CREATE OR ALTER PROCEDURE [dbo].[AccessAuditEvent_ReadManyByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @Since DATETIME2(7),
    @BeforeOccurredAt DATETIME2(7) = NULL,
    @BeforeId UNIQUEIDENTIFIER = NULL,
    @Take INT = 25
AS
BEGIN
    SET NOCOUNT ON

    -- Reads one page of the PAM access-audit trail for an entire organization from the append-only [AccessAuditEvent]
    -- store: stored events on or after @Since, newest first. Fully SELF-CONTAINED, because every display name was
    -- resolved and frozen into the row at write time (see AccessAuditEvent_Create), so this read touches no other table
    -- and a later delete or rename of a referenced entity cannot erase or rewrite the event. Org-scoped: the caller is
    -- authorized by the AccessEventLogs permission at the endpoint. [Kind], [Phase], [RotationSource], and [SyncState]
    -- hold Bit.Pam.Enums.AccessAuditEventKind, AccessAuditEventPhase, PamRotationSource, and PamRotationSyncState.
    --
    -- Paging is keyset, not OFFSET. (@BeforeOccurredAt, @BeforeId) is the last row the caller already has, and the
    -- predicate seeks straight to that position in IX_AccessAuditEvent_OrganizationId_OccurredAt_Id, so every page
    -- costs the same no matter how deep it is. An OFFSET would instead re-serve rows: the store is append-only and read
    -- newest first, so each event written between two requests shifts the window down by one and pushes a row the
    -- caller has already seen onto the next page. Both cursor halves are needed because [OccurredAt] is not unique,
    -- since an action's Attempt and Outcome are written with the same timestamp; [Id] breaks that tie and is the third
    -- index key so the ORDER BY is satisfied by the index rather than by sorting the whole matched range.
    SELECT
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
        [RuleName],
        [TargetSystemId],
        [TargetSystemName],
        [DaemonId],
        [DaemonName],
        [RotationConfigId],
        [RotationJobId],
        [RotationSource],
        [SyncState]
    FROM [dbo].[AccessAuditEvent]
    WHERE [OrganizationId] = @OrganizationId
        AND [OccurredAt] >= @Since
        AND (@BeforeOccurredAt IS NULL
             OR [OccurredAt] < @BeforeOccurredAt
             OR ([OccurredAt] = @BeforeOccurredAt AND [Id] < @BeforeId))
    ORDER BY [OccurredAt] DESC, [Id] DESC
    OFFSET 0 ROWS
    FETCH NEXT @Take ROWS ONLY
END
GO
