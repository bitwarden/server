CREATE PROCEDURE [dbo].[AccessAuditEvent_ReadItemsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @StartDate DATETIME2(7),
    @EndDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- What the trail's Item filter is built from. Neither obvious source works: a page of the trail cannot name
    -- every item in range, and the caller's own vault would offer every credential they hold whether the trail
    -- mentions it or not. No cipher name is returned, because the store holds none; the caller resolves it from its
    -- own vault. Ranked rather than aggregated so each subject carries its most recent context, where MIN/MAX would
    -- pick alphabetically and for a renamed rule that is the wrong name.
    ;WITH [Ciphers] AS (
        SELECT
            [CipherId],
            [CollectionId],
            ROW_NUMBER() OVER (PARTITION BY [CipherId] ORDER BY [OccurredDate] DESC, [Id] DESC) AS [Rank]
        FROM
            [dbo].[AccessAuditEvent]
        WHERE
            [OrganizationId] = @OrganizationId
            AND [OccurredDate] >= @StartDate
            AND [OccurredDate] <= @EndDate
            AND [CipherId] IS NOT NULL
    ),
    [Rules] AS (
        SELECT
            [AccessRuleId],
            [RuleName],
            ROW_NUMBER() OVER (PARTITION BY [AccessRuleId] ORDER BY [OccurredDate] DESC, [Id] DESC) AS [Rank]
        FROM
            [dbo].[AccessAuditEvent]
        WHERE
            [OrganizationId] = @OrganizationId
            AND [OccurredDate] >= @StartDate
            AND [OccurredDate] <= @EndDate
            AND [AccessRuleId] IS NOT NULL
    )
    SELECT
        [CipherId],
        [CollectionId],
        CAST(NULL AS UNIQUEIDENTIFIER) AS [RuleId],
        CAST(NULL AS NVARCHAR(256)) AS [RuleName]
    FROM
        [Ciphers]
    WHERE
        [Rank] = 1

    UNION ALL

    SELECT
        NULL,
        NULL,
        [AccessRuleId],
        [RuleName]
    FROM
        [Rules]
    WHERE
        [Rank] = 1
END
