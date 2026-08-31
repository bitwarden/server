CREATE PROCEDURE [dbo].[AccessAuditEvent_ReadManyByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @Since DATETIME2(7),
    @BeforeOccurredDate DATETIME2(7) = NULL,
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
    -- Paging is keyset, not OFFSET. (@BeforeOccurredDate, @BeforeId) is the last row the caller already has, and the
    -- predicate seeks straight to that position in IX_AccessAuditEvent_OrganizationId_OccurredDate_Id, so every page
    -- costs the same no matter how deep it is. An OFFSET would instead re-serve rows: the store is append-only and read
    -- newest first, so each event written between two requests shifts the window down by one and pushes a row the
    -- caller has already seen onto the next page. Both cursor halves are needed because [OccurredDate] is not unique,
    -- since an action's Attempt and Outcome are written with the same timestamp; [Id] breaks that tie and is the third
    -- index key so the ORDER BY is satisfied by the index rather than by sorting the whole matched range.
    SELECT
        [Id],
        [Kind],
        [Phase],
        [CorrelationId],
        [OccurredDate],
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
        AND [OccurredDate] >= @Since
        AND (@BeforeOccurredDate IS NULL
             OR [OccurredDate] < @BeforeOccurredDate
             OR ([OccurredDate] = @BeforeOccurredDate AND [Id] < @BeforeId))
    ORDER BY [OccurredDate] DESC, [Id] DESC
    OFFSET 0 ROWS
    FETCH NEXT @Take ROWS ONLY
END
