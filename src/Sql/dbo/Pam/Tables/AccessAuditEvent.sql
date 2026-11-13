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

-- Subject and rotation ids are deliberately not foreign keyed, so an event outlives a delete or rename of what it
-- references. Every stored name is plaintext: the subject cipher and collection are recorded by id only, so no vault
-- data lands here.
--
-- [CorrelationId] deliberately has no default, here and on the three EF providers. An action's Attempt and Outcome
-- must share one id for the read to collapse them, so the caller mints one per action; a DEFAULT NEWID() would let a
-- forgotten id become a fresh one that correlates with nothing, which reads back as a lone in-doubt half rather than
-- as an error.
--
-- [Id] is the third key column because [OccurredDate] is not unique: an action's Attempt and Outcome share a
-- timestamp, and a page boundary landing among them cannot be resumed without a tiebreaker. The INCLUDE covers the
-- collapse and the page read's filters, so a filtered page tests each candidate inside the index instead of opening
-- the row, and AccessAuditEvent_ReadItemsByOrganizationId finds the subjects in range here. The INCLUDE has no EF
-- equivalent to mirror onto the other three databases.
CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_OrganizationId_OccurredDate_Id]
    ON [dbo].[AccessAuditEvent] ([OrganizationId] ASC, [OccurredDate] DESC, [Id] DESC)
    INCLUDE ([CorrelationId], [Phase], [CipherId], [CollectionId], [AccessRuleId], [RuleName],
        [Kind], [ActorId], [RequesterId]);
GO

-- One per subject column, so AccessAuditEvent_ReadItemsByOrganizationId reaches a subject's most recent event in
-- range with one seek. Filtered to the events that name the subject: most name a cipher, few name a rule.
CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_OrganizationId_CipherId_OccurredDate_Id]
    ON [dbo].[AccessAuditEvent] ([OrganizationId] ASC, [CipherId] ASC, [OccurredDate] DESC, [Id] DESC)
    INCLUDE ([CollectionId])
    WHERE [CipherId] IS NOT NULL;
GO

CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_OrganizationId_AccessRuleId_OccurredDate_Id]
    ON [dbo].[AccessAuditEvent] ([OrganizationId] ASC, [AccessRuleId] ASC, [OccurredDate] DESC, [Id] DESC)
    INCLUDE ([RuleName])
    WHERE [AccessRuleId] IS NOT NULL;
GO

CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_CorrelationId]
    ON [dbo].[AccessAuditEvent] ([CorrelationId] ASC)
    INCLUDE ([OrganizationId], [OccurredDate], [Phase]);
GO
