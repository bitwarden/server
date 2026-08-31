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
GO

-- Append-only audit store. State-changing PAM actions insert here via AccessAuditEvent_Create, which snapshots the
-- actor, requester, rule, target system, and daemon display names into the row at write time so each event is
-- SELF-CONTAINED; the trail is then read back org-scoped and newest-first with no joins. Every stored name is
-- plaintext, so the subject cipher and collection are recorded by id only and no vault data lands in the audit store.
-- Subject and rotation ids are deliberately NOT foreign keyed so an event survives deletion of what it references, and
-- the frozen names mean a later delete or rename cannot rewrite history. The rotation columns are NULL for
-- non-rotation events.
--
-- The trail's own read: org-scoped, ranged on [OccurredDate], newest first, one page at a time. [Id] is the third key
-- column purely so that order comes straight off the index: [OccurredDate] alone is not unique (an action's Attempt
-- and Outcome share a timestamp), and without a tiebreaker in the key a page boundary landing among events that share
-- an instant cannot be resumed exactly.
--
-- [CorrelationId] and [Phase] are what the before/after collapse tests each candidate row on -- without them every row
-- considered for a page, not just the ones returned, would cost a key lookup. The four subject columns after them
-- cover AccessAuditEvent_ReadItemsByOrganizationId, which reads the whole range rather than a page and would otherwise
-- pay that lookup on every row of it. They ride here rather than on an index of their own because the page read is a
-- TOP-N seek -- it stops as soon as it has filled a page -- so a wider leaf row costs it almost nothing, where a
-- second index would cost every insert, and INCLUDE has no Entity Framework equivalent to mirror onto the other three
-- databases.
CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_OrganizationId_OccurredDate_Id]
    ON [dbo].[AccessAuditEvent] ([OrganizationId] ASC, [OccurredDate] DESC, [Id] DESC)
    INCLUDE ([CorrelationId], [Phase], [CipherId], [CollectionId], [AccessRuleId], [RuleName]);
GO

-- Serves the collapse itself, which asks "is there a further-along half of this action?" once per candidate row. A
-- correlation holds one or two rows, so this is a point lookup; without it the question would be a scan.
CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_CorrelationId]
    ON [dbo].[AccessAuditEvent] ([CorrelationId] ASC)
    INCLUDE ([OrganizationId], [OccurredDate], [Phase]);
GO
