CREATE PROCEDURE [dbo].[AccessAuditEvent_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CorrelationId UNIQUEIDENTIFIER,
    @Kind TINYINT,
    @Phase TINYINT,
    @OccurredDate DATETIME2(7),
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
    -- rename cannot change what this event says. Actor and requester names are resolved by id from [User] here and
    -- frozen, staying NULL where the id is NULL or the row is gone. The rule, target system, and daemon names come from
    -- the caller instead of a JOIN, because those entities can be deleted or renamed in the same action. The subject
    -- cipher and collection are recorded by id alone; their names are vault data, which this store never holds.
    INSERT INTO [dbo].[AccessAuditEvent]
    (
        [Id],
        [OrganizationId],
        [CorrelationId],
        [Kind],
        [Phase],
        [OccurredDate],
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
    )
    SELECT
        @Id,
        @OrganizationId,
        @CorrelationId,
        @Kind,
        @Phase,
        @OccurredDate,
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
END
