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
GO

-- Append-only audit store. State-changing PAM actions insert here via AccessAuditEvent_Create, which snapshots the
-- actor, requester, rule, target system, and daemon display names into the row at write time so each event is
-- SELF-CONTAINED; the trail is then read back org-scoped and newest-first with no joins. Every stored name is
-- plaintext, so the subject cipher and collection are recorded by id only and no vault data lands in the audit store.
-- Subject and rotation ids are deliberately NOT foreign keyed so an event survives deletion of what it references, and
-- the frozen names mean a later delete or rename cannot rewrite history. The rotation columns are NULL for
-- non-rotation events.
--
-- [Id] is the third key column purely so the paged read's ORDER BY comes straight off the index: OccurredAt alone is
-- not unique (an action's Attempt and Outcome share a timestamp), and without a tiebreaker in the key an OFFSET page
-- can serve the same row twice or skip it entirely.
CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_OrganizationId_OccurredAt_Id]
    ON [dbo].[AccessAuditEvent] ([OrganizationId] ASC, [OccurredAt] DESC, [Id] DESC);
GO
