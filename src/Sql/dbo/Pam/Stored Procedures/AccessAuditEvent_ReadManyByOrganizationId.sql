CREATE PROCEDURE [dbo].[AccessAuditEvent_ReadManyByOrganizationId]
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
