CREATE PROCEDURE [dbo].[AccessAuditEvent_Create]
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

    -- Snapshots names at write; later deletes/renames can't change the event.
    -- Rule/target-system/daemon names are caller-supplied, not JOINed, since those rows may be deleted.
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
