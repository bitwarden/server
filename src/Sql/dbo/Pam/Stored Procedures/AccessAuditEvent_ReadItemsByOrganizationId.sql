CREATE PROCEDURE [dbo].[AccessAuditEvent_ReadItemsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @StartDate DATETIME2(7),
    @EndDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON;

    -- The subjects the trail's Item filter offers; the caller resolves cipher names from its own vault. Ranked rather
    -- than aggregated, so a renamed rule carries its newest name where MIN/MAX would pick alphabetically.
    WITH [Ciphers] AS (
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
