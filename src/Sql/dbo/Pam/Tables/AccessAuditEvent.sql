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
    CONSTRAINT [PK_AccessAuditEvent] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_AccessAuditEvent_Organization] FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
);
GO

-- Append-only audit store. State-changing PAM actions insert here via AccessAuditEvent_Create, which snapshots the
-- actor/requester/cipher/collection/rule display names into the row at write time so each event is SELF-CONTAINED; the
-- trail is then read back org-scoped and newest-first with no joins. Subject ids are deliberately NOT foreign keyed so
-- an event survives deletion of what it references, and the frozen names mean a later delete or rename cannot rewrite
-- history. Cipher/collection names are encrypted (EncString), decrypted client-side.
CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_OrganizationId_OccurredAt]
    ON [dbo].[AccessAuditEvent] ([OrganizationId] ASC, [OccurredAt] DESC);
GO
