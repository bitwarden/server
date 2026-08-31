CREATE PROCEDURE [dbo].[AccessAuditEvent_ReadItemsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @StartDate DATETIME2(7),
    @EndDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The distinct subjects the organization's access-audit trail names between @StartDate and @EndDate: one row per
    -- cipher, one per access rule. This is what the trail's Item filter is built from, and it exists because neither
    -- of the two obvious sources works -- a page of the trail holds fifty rows and cannot name every item in range,
    -- and the caller's own vault would offer every credential they hold whether the trail mentions it or not.
    --
    -- No cipher NAME is returned. [CipherName] is Vault Data (an EncString) that an auditor generally cannot decrypt,
    -- so the caller resolves names from its own vault and drops the ones it cannot read. [RuleName] IS returned:
    -- plaintext organization configuration, snapshotted per event, so it travels with the id.
    --
    -- Ranked rather than aggregated so each subject carries its MOST RECENT context -- a renamed rule reads in the
    -- menu the way the newest rows read in the table, and a cipher's collection is the one it was last gated through.
    -- MIN/MAX would pick alphabetically, which for a rename is simply the wrong name.
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
