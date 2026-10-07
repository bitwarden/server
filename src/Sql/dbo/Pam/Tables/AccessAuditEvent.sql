CREATE TABLE [dbo].[AccessAuditEvent] (
    [Id]                  UNIQUEIDENTIFIER    NOT NULL,
    [OrganizationId]      UNIQUEIDENTIFIER    NOT NULL,
    [CorrelationId]       UNIQUEIDENTIFIER    NOT NULL,
    [Kind]                TINYINT             NOT NULL,
    [Phase]               TINYINT             NOT NULL,
    [OccurredDate]        DATETIME2(7)        NOT NULL,
    [ActorId]             UNIQUEIDENTIFIER    NULL,
    [RequesterId]         UNIQUEIDENTIFIER    NULL,
    [CollectionId]        UNIQUEIDENTIFIER    NULL,
    [CipherId]            UNIQUEIDENTIFIER    NULL,
    [AccessRequestId]     UNIQUEIDENTIFIER    NULL,
    [AccessLeaseId]       UNIQUEIDENTIFIER    NULL,
    [AccessRuleId]        UNIQUEIDENTIFIER    NULL,
    [Detail]              NVARCHAR(MAX)       NULL,
    [LeaseNotBefore]      DATETIME2(7)        NULL,
    [LeaseNotAfter]       DATETIME2(7)        NULL,
    [ActorName]           NVARCHAR(50)        NULL,
    [ActorEmail]          NVARCHAR(256)       NULL,
    [RequesterName]       NVARCHAR(50)        NULL,
    [RequesterEmail]      NVARCHAR(256)       NULL,
    [RuleName]            NVARCHAR(256)       NULL,
    [TargetSystemId]      UNIQUEIDENTIFIER    NULL,
    [TargetSystemName]    NVARCHAR(200)       NULL,
    [AccessConnectorId]   UNIQUEIDENTIFIER    NULL,
    [AccessConnectorName] NVARCHAR(200)       NULL,
    [RotationConfigId]    UNIQUEIDENTIFIER    NULL,
    [RotationJobId]       UNIQUEIDENTIFIER    NULL,
    [RotationSource]      TINYINT             NULL,
    [SyncState]           TINYINT             NULL,
    CONSTRAINT [PK_AccessAuditEvent] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_AccessAuditEvent_Organization] FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
);
GO

-- Subject and rotation ids have no foreign keys, so an event outlives what it references. [CorrelationId] has no
-- default, here or in EF, so a forgotten id fails instead of reading back as a lone in-doubt half.

-- [Id] breaks ties within an [OccurredDate], so a page can resume between an action's two halves. The INCLUDE lets
-- the collapse and the filters test candidates inside the index; EF has no equivalent.
CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_OrganizationId_OccurredDate_Id]
    ON [dbo].[AccessAuditEvent] ([OrganizationId] ASC, [OccurredDate] DESC, [Id] DESC)
    INCLUDE ([CorrelationId], [Phase], [CipherId], [CollectionId], [AccessRuleId], [RuleName],
        [Kind], [ActorId], [RequesterId]);
GO

CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_CorrelationId]
    ON [dbo].[AccessAuditEvent] ([CorrelationId] ASC)
    INCLUDE ([OrganizationId], [OccurredDate], [Phase]);
GO
