CREATE PROCEDURE [dbo].[AccessAuditEvent_ReadPageByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    -- The window being audited. Inclusive, and identical on every page of a walk.
    @StartDate DATETIME2(7),
    @EndDate DATETIME2(7),
    @PageSize INT,
    -- The keyset cursor: the previous page's last row, or NULL for the newest event. Kept apart from @EndDate because
    -- resuming needs the [Id] tiebreaker, and narrowing the window per page would change which half survives.
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
        -- Collapse each action to its Outcome, or its lone Attempt, which the caller flags as in-doubt. Done here
        -- because one page cannot tell a missing Outcome from one on the next page; an action straddling a bound
        -- reads as in-doubt.
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
        -- Filters apply after the collapse, since an action's halves can disagree: a refused activation writes
        -- LeaseActivated, then LeaseActivationRejected.
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
