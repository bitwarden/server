-- Add the PAM access-audit log: the AccessAuditEvent store and its two stored procedures. Consolidated net-new
-- migration (the feature has not shipped), squashing the incremental steps the store went through during development.
--
-- The store is append-only and SELF-CONTAINED: AccessAuditEvent_Create snapshots the actor / requester / cipher /
-- collection / rule display names into the row at write time, so the trail read touches no other table and a later
-- delete or rename cannot erase or rewrite history. Subject ids are deliberately NOT foreign keyed for the same
-- reason -- an event outlives what it references. Only OrganizationId is, so the rows go when the org does.

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
        [CipherName]        NVARCHAR(MAX)       NULL,
        [CollectionName]    NVARCHAR(MAX)       NULL,
        [RuleName]          NVARCHAR(256)       NULL,
        [CorrelationId]     UNIQUEIDENTIFIER    NOT NULL CONSTRAINT [DF_AccessAuditEvent_CorrelationId] DEFAULT NEWID(),
        CONSTRAINT [PK_AccessAuditEvent] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AccessAuditEvent_Organization] FOREIGN KEY ([OrganizationId])
            REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
    );
END
GO

-- Serves AccessAuditEvent_ReadManyByOrganizationId, which is the only read: org-scoped, filtered on OccurredAt, and
-- returned newest first. The DESC key order lets the ORDER BY come straight off the index.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessAuditEvent_OrganizationId_OccurredAt' AND object_id = OBJECT_ID('[dbo].[AccessAuditEvent]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_OrganizationId_OccurredAt]
        ON [dbo].[AccessAuditEvent] ([OrganizationId] ASC, [OccurredAt] DESC);
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
    @LeaseNotAfter DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Snapshot the display names into the row at write time so the audit event is self-contained: a later delete or
    -- rename cannot change what this event says. Actor/requester/cipher/collection names are resolved by id from the
    -- live tables once, here, and frozen (cipher/collection names are encrypted EncString, stored as-is for the client
    -- to decrypt); a name is NULL where its id is NULL or the row is gone. The rule name is supplied by the caller
    -- (@RuleName), not JOINed -- a rule can be hard-deleted in the same action, so its name is captured before then.
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
        [CipherName],
        [CollectionName],
        [RuleName]
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
        JSON_VALUE(C.[Data], '$.Name'),
        COL.[Name],
        @RuleName
    FROM (SELECT 1 AS [X]) Seed
    LEFT JOIN [dbo].[User] AU ON AU.[Id] = @ActorId
    LEFT JOIN [dbo].[User] RU ON RU.[Id] = @RequesterId
    LEFT JOIN [dbo].[Cipher] C ON C.[Id] = @CipherId
    LEFT JOIN [dbo].[Collection] COL ON COL.[Id] = @CollectionId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessAuditEvent_ReadManyByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @Since DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Reads the PAM access-audit trail for an entire organization from the append-only [AccessAuditEvent] store: every
    -- stored event on or after @Since, newest first. Fully SELF-CONTAINED -- the actor/requester/cipher/collection/rule
    -- display names were resolved and frozen into the row at write time (see AccessAuditEvent_Create), so this read
    -- touches no other table and a later delete or rename of a referenced entity cannot erase or rewrite the event.
    -- Cipher/collection names are encrypted (EncString), decrypted client-side. Org-scoped: the caller is authorized by
    -- the AccessEventLogs permission at the endpoint. Kind matches Bit.Pam.Enums.AccessAuditEventKind; Phase matches
    -- Bit.Pam.Enums.AccessAuditEventPhase. Time-derived expiry kinds are not written by any action yet (deferred).
    SELECT
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
        [RuleName]
    FROM [dbo].[AccessAuditEvent]
    WHERE [OrganizationId] = @OrganizationId
        AND [OccurredAt] >= @Since
    ORDER BY [OccurredAt] DESC
END
GO
