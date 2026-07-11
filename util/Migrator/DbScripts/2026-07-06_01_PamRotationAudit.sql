-- Extend the PAM append-only access-audit event store ([dbo].[AccessAuditEvent]) to carry credential-rotation events
-- (rotation lifecycle + fleet/target administration -- see Bit.Pam.Enums.AccessAuditEventKind 50-79). Same
-- self-contained model as the existing store: TargetSystemName/DaemonName are supplied by the rotation commands
-- (snapshotted at write, same pattern as RuleName), not JOINed -- a target system or daemon can be deleted in the same
-- action. RotationConfigId/RotationJobId/RotationSource/SyncState are stored as-is (SyncState/RotationSource match
-- Bit.Pam.Enums.PamRotationSyncState/PamRotationSource). Dapper/MSSQL only, like the rest of the PAM POC.

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
    -- rename cannot change what this event says. Actor/requester/cipher/collection names are resolved by id from the
    -- live tables once, here, and frozen (cipher/collection names are encrypted EncString, stored as-is for the client
    -- to decrypt); a name is NULL where its id is NULL or the row is gone. The rule/target-system/daemon names are
    -- supplied by the caller (@RuleName/@TargetSystemName/@DaemonName), not JOINed -- those entities can be deleted or
    -- renamed in the same action, so their names are captured by the command before then.
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
        JSON_VALUE(C.[Data], '$.Name'),
        COL.[Name],
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
    -- stored event on or after @Since, newest first. Fully SELF-CONTAINED -- the actor/requester/cipher/collection/rule/
    -- target-system/daemon display names were resolved and frozen into the row at write time (see
    -- AccessAuditEvent_Create), so this read touches no other table and a later delete or rename of a referenced entity
    -- cannot erase or rewrite the event. Cipher/collection names are encrypted (EncString), decrypted client-side.
    -- Org-scoped: the caller is authorized by the AccessEventLogs permission at the endpoint. Kind matches
    -- Bit.Pam.Enums.AccessAuditEventKind; Phase matches Bit.Pam.Enums.AccessAuditEventPhase; RotationSource matches
    -- Bit.Pam.Enums.PamRotationSource; SyncState matches Bit.Pam.Enums.PamRotationSyncState. Time-derived expiry kinds
    -- are not written by any action yet (deferred).
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
    ORDER BY [OccurredAt] DESC
END
GO
