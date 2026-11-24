-- Records the source IP address and device type of the request behind each PAM audit event, and lets an automatic
-- lease extension record the kind of condition it was decided on.
IF COL_LENGTH('[dbo].[AccessAuditEvent]', 'IpAddress') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessAuditEvent] ADD [IpAddress] VARCHAR(50) NULL;
END
GO

IF COL_LENGTH('[dbo].[AccessAuditEvent]', 'DeviceType') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessAuditEvent] ADD [DeviceType] TINYINT NULL;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessAuditEvent_Create]
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
    @AccessConnectorId UNIQUEIDENTIFIER = NULL,
    @AccessConnectorName NVARCHAR(200) = NULL,
    @RotationConfigId UNIQUEIDENTIFIER = NULL,
    @RotationJobId UNIQUEIDENTIFIER = NULL,
    @RotationSource TINYINT = NULL,
    @SyncState TINYINT = NULL,
    @IpAddress VARCHAR(50) = NULL,
    @DeviceType TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- The names are snapshotted so a later delete or rename cannot change what this event says. Actor and requester
    -- resolve from [User] here; the rule, target system, and access connector names come from the caller instead,
    -- because those entities can be deleted in the same action. The cipher and collection are recorded by id alone:
    -- their names are vault data, which this store never holds.
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
        [AccessConnectorId],
        [AccessConnectorName],
        [RotationConfigId],
        [RotationJobId],
        [RotationSource],
        [SyncState],
        [IpAddress],
        [DeviceType]
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
        @AccessConnectorId,
        @AccessConnectorName,
        @RotationConfigId,
        @RotationJobId,
        @RotationSource,
        @SyncState,
        @IpAddress,
        @DeviceType
    FROM
        (SELECT 1 AS [X]) Seed
    LEFT JOIN
        [dbo].[User] AU ON AU.[Id] = @ActorId
    LEFT JOIN
        [dbo].[User] RU ON RU.[Id] = @RequesterId
END

GO

CREATE OR ALTER PROCEDURE [dbo].[AccessAuditEvent_ReadPageByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    -- The window being audited. Inclusive, and identical on every page of a walk.
    @StartDate DATETIME2(7),
    @EndDate DATETIME2(7),
    @PageSize INT,
    -- The keyset cursor: the last row of the previous page, so it moves inward while the window above stays put,
    -- and NULL starts at the newest event in range. A separate pair rather than a lowered @EndDate, because
    -- [OccurredDate] is not unique so resuming needs the [Id] tiebreaker, and because the collapse below is scoped
    -- to the window, so narrowing @EndDate per page would change which half of an action survives.
    @BeforeDate DATETIME2(7) = NULL,
    @BeforeId UNIQUEIDENTIFIER = NULL,
    -- JSON arrays ([1,13,30], ["<guid>",...]); NULL means the dimension is unfiltered. OPENJSON because [Kind] is a
    -- TINYINT and no matching table-valued type exists.
    @Kinds NVARCHAR(MAX) = NULL,
    @ActorIds NVARCHAR(MAX) = NULL,
    @IncludeAutomatedActor BIT = 0,
    @RequesterIds NVARCHAR(MAX) = NULL,
    @CipherIds NVARCHAR(MAX) = NULL,
    @RuleIds NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP (@PageSize)
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
        [AccessConnectorId],
        [AccessConnectorName],
        [RotationConfigId],
        [RotationJobId],
        [RotationSource],
        [SyncState],
        [IpAddress],
        [DeviceType]
    FROM
        [dbo].[AccessAuditEvent] E
    WHERE
        E.[OrganizationId] = @OrganizationId
        AND E.[OccurredDate] >= @StartDate
        AND E.[OccurredDate] <= @EndDate
        AND (
            @BeforeDate IS NULL
            OR E.[OccurredDate] < @BeforeDate
            OR (E.[OccurredDate] = @BeforeDate AND E.[Id] < @BeforeId)
        )
        -- Collapse each action's Attempt/Outcome pair into one row: the Outcome when it landed, otherwise the lone
        -- Attempt, which the caller flags as in-doubt. It happens here because the caller sees one page and could
        -- not tell an Attempt whose Outcome sits on the next page from one that never landed. Scoped to the page's
        -- range, so an action straddling a bound reads as in-doubt at that edge instead of disappearing.
        AND NOT EXISTS (
            SELECT
                1
            FROM
                [dbo].[AccessAuditEvent] P
            WHERE
                P.[CorrelationId] = E.[CorrelationId]
                AND P.[OrganizationId] = @OrganizationId
                AND P.[OccurredDate] >= @StartDate
                AND P.[OccurredDate] <= @EndDate
                AND (
                    P.[Phase] > E.[Phase]
                    OR (P.[Phase] = E.[Phase] AND P.[Id] < E.[Id])
                )
        )
        -- The dimensions below apply to whichever row survived the collapse, because the two halves of one action
        -- need not agree: a refused activation writes LeaseActivated then LeaseActivationRejected, so filtering
        -- first would answer "activated" with an action that was turned down.
        AND (
            @Kinds IS NULL
            OR E.[Kind] IN (SELECT CAST([value] AS TINYINT) FROM OPENJSON(@Kinds))
        )
        AND (
            (@ActorIds IS NULL AND @IncludeAutomatedActor = 0)
            OR (@IncludeAutomatedActor = 1 AND E.[ActorId] IS NULL)
            OR (
                @ActorIds IS NOT NULL
                AND E.[ActorId] IN (SELECT CAST([value] AS UNIQUEIDENTIFIER) FROM OPENJSON(@ActorIds))
            )
        )
        AND (
            @RequesterIds IS NULL
            OR E.[RequesterId] IN (SELECT CAST([value] AS UNIQUEIDENTIFIER) FROM OPENJSON(@RequesterIds))
        )
        -- The two Item columns union rather than narrow: a rule-administration event names a rule and no cipher.
        AND (
            (@CipherIds IS NULL AND @RuleIds IS NULL)
            OR (
                @CipherIds IS NOT NULL
                AND E.[CipherId] IN (SELECT CAST([value] AS UNIQUEIDENTIFIER) FROM OPENJSON(@CipherIds))
            )
            OR (
                @RuleIds IS NOT NULL
                AND E.[AccessRuleId] IN (SELECT CAST([value] AS UNIQUEIDENTIFIER) FROM OPENJSON(@RuleIds))
            )
        )
    ORDER BY
        E.[OccurredDate] DESC,
        E.[Id] DESC
END

GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_CreateApprovedExtension]
    @AccessRequestId UNIQUEIDENTIFIER,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @ExtensionOfLeaseId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @RequesterId UNIQUEIDENTIFIER,
    @NotBefore DATETIME2(7),
    @NotAfter DATETIME2(7),
    @Reason NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7),
    @RuleId UNIQUEIDENTIFIER = NULL,
    @DenialComment NVARCHAR(MAX) = NULL,
    @ConditionKind TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON
    -- Holds the per-lease range lock until writes commit, serializing concurrent extensions.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- Locks the parent lease; must have no early end and be in-window to extend.
    IF NOT EXISTS (
        SELECT 1
        FROM [dbo].[AccessLease] WITH (UPDLOCK, HOLDLOCK)
        WHERE [Id] = @ExtensionOfLeaseId
            AND [RequesterId] = @RequesterId
            AND [Action] = 0 /* None (no early end) */
            AND [NotAfter] > @Now
    )
    BEGIN
        -- Records a denied, answerable request that still counts toward the extension cap.
        INSERT INTO [dbo].[AccessRequest]
        (
            [Id], [ExtensionOfLeaseId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
            [NotBefore], [NotAfter], [Reason], [Action], [CreationDate], [ActionDate], [RuleId]
        )
        VALUES
        (
            @AccessRequestId, @ExtensionOfLeaseId, @OrganizationId, @CollectionId, @CipherId, @RequesterId,
            @NotBefore, @NotAfter, @Reason, 2 /* Denied */, @Now, @Now, @RuleId
        )

        INSERT INTO [dbo].[AccessDecision]
        (
            [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
            [Verdict], [Comment], [EvaluationContext], [CreationDate]
        )
        VALUES
        (
            @AccessDecisionId, @AccessRequestId, 0 /* Automatic */, NULL, NULL,
            0 /* Deny */, @DenialComment, NULL, @Now
        )

        COMMIT TRANSACTION
        SELECT 0 -- LeaseNotActive
        RETURN
    END

    -- Extension is capped at one per lease; counted under the same lock, race-safe.
    IF EXISTS (SELECT 1 FROM [dbo].[AccessRequest] WHERE [ExtensionOfLeaseId] = @ExtensionOfLeaseId)
    BEGIN
        ROLLBACK TRANSACTION
        SELECT -1 -- AlreadyExtended
        RETURN
    END

    -- No new lease is minted; extending pushes the parent lease's NotAfter out in place.
    INSERT INTO [dbo].[AccessRequest]
    (
        [Id], [ExtensionOfLeaseId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [NotBefore], [NotAfter], [Reason], [Action], [CreationDate], [ActionDate], [RuleId]
    )
    VALUES
    (
        @AccessRequestId, @ExtensionOfLeaseId, @OrganizationId, @CollectionId, @CipherId, @RequesterId,
        @NotBefore, @NotAfter, @Reason, 1 /* Approved */, @Now, @Now, @RuleId
    )

    INSERT INTO [dbo].[AccessDecision]
    (
        [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
        [Verdict], [Comment], [EvaluationContext], [CreationDate]
    )
    VALUES
    (
        @AccessDecisionId, @AccessRequestId, 0 /* Automatic */, NULL, @ConditionKind,
        1 /* Approve */, NULL, NULL, @Now
    )

    UPDATE [dbo].[AccessLease]
    SET [NotAfter] = @NotAfter
    WHERE [Id] = @ExtensionOfLeaseId

    COMMIT TRANSACTION

    SELECT 1 -- Extended
END
GO
