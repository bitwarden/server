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
    [CipherName]        NVARCHAR(MAX)       NULL,
    [CollectionName]    NVARCHAR(MAX)       NULL,
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

-- Append-only; names snapshot at write, so rows stay self-contained.
-- Subject ids aren't FKed; a row survives deletion of what it references.
-- Serves the org-scoped page read; INCLUDEs avoid extra key lookups for the collapse.
CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_OrganizationId_OccurredAt_Id]
    ON [dbo].[AccessAuditEvent] ([OrganizationId] ASC, [OccurredAt] DESC, [Id] DESC)
    INCLUDE ([CorrelationId], [Phase], [CipherId], [CollectionId], [AccessRuleId], [RuleName]);
GO

-- Serves the collapse's per-row lookup for a correlation's other half (1-2 rows).
CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_CorrelationId]
    ON [dbo].[AccessAuditEvent] ([CorrelationId] ASC)
    INCLUDE ([OrganizationId], [OccurredAt], [Phase]);
GO
