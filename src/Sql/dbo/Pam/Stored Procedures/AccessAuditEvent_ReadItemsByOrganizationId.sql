CREATE PROCEDURE [dbo].[AccessAuditEvent_ReadItemsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @StartDate DATETIME2(7),
    @EndDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Distinct subjects (ciphers, rules) in range, ranked for each one's most recent name.
    -- Cipher names are encrypted and omitted; rule names are plaintext and returned.
    ;WITH [Ciphers] AS (
        SELECT
            [CipherId],
            [CollectionId],
            ROW_NUMBER() OVER (PARTITION BY [CipherId] ORDER BY [OccurredAt] DESC, [Id] DESC) AS [Rank]
        FROM [dbo].[AccessAuditEvent]
        WHERE [OrganizationId] = @OrganizationId
            AND [OccurredAt] >= @StartDate
            AND [OccurredAt] <= @EndDate
            AND [CipherId] IS NOT NULL
    ),
    [Rules] AS (
        SELECT
            [AccessRuleId],
            [RuleName],
            ROW_NUMBER() OVER (PARTITION BY [AccessRuleId] ORDER BY [OccurredAt] DESC, [Id] DESC) AS [Rank]
        FROM [dbo].[AccessAuditEvent]
        WHERE [OrganizationId] = @OrganizationId
            AND [OccurredAt] >= @StartDate
            AND [OccurredAt] <= @EndDate
            AND [AccessRuleId] IS NOT NULL
    )
    SELECT
        [CipherId],
        [CollectionId],
        CAST(NULL AS UNIQUEIDENTIFIER) AS [RuleId],
        CAST(NULL AS NVARCHAR(256)) AS [RuleName]
    FROM [Ciphers]
    WHERE [Rank] = 1

    UNION ALL

    SELECT
        NULL,
        NULL,
        [AccessRuleId],
        [RuleName]
    FROM [Rules]
    WHERE [Rank] = 1
END
