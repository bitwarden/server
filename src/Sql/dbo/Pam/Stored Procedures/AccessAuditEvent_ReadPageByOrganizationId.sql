CREATE PROCEDURE [dbo].[AccessAuditEvent_ReadPageByOrganizationId]
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
        [SyncState]
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
