-- Subject indexes for AccessAuditEvent_ReadItemsByOrganizationId, which now seeks each subject's most recent event
-- in range instead of ranking every event in range.
IF NOT EXISTS (
    SELECT
        NULL
    FROM
        sys.indexes
    WHERE
        [name] = 'IX_AccessAuditEvent_OrganizationId_CipherId_OccurredDate_Id'
        AND object_id = OBJECT_ID('[dbo].[AccessAuditEvent]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_OrganizationId_CipherId_OccurredDate_Id]
    ON [dbo].[AccessAuditEvent] ([OrganizationId] ASC, [CipherId] ASC, [OccurredDate] DESC, [Id] DESC)
    INCLUDE ([CollectionId])
    WHERE [CipherId] IS NOT NULL;
END
GO

IF NOT EXISTS (
    SELECT
        NULL
    FROM
        sys.indexes
    WHERE
        [name] = 'IX_AccessAuditEvent_OrganizationId_AccessRuleId_OccurredDate_Id'
        AND object_id = OBJECT_ID('[dbo].[AccessAuditEvent]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_OrganizationId_AccessRuleId_OccurredDate_Id]
    ON [dbo].[AccessAuditEvent] ([OrganizationId] ASC, [AccessRuleId] ASC, [OccurredDate] DESC, [Id] DESC)
    INCLUDE ([RuleName])
    WHERE [AccessRuleId] IS NOT NULL;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessAuditEvent_ReadItemsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @StartDate DATETIME2(7),
    @EndDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON;

    -- What the trail's Item filter is built from. Neither obvious source works: a page of the trail cannot name
    -- every item in range, and the caller's own vault would offer every credential they hold whether the trail
    -- mentions it or not. No cipher name is returned, because the store holds none; the caller resolves it from its
    -- own vault. Each subject carries the context of its most recent event in range, where MIN/MAX would pick
    -- alphabetically and for a renamed rule that is the wrong name. That event is one seek on the subject's index,
    -- so only the distinct subjects are ranked, not every event in range.
    SELECT
        C.[CipherId],
        L.[CollectionId],
        CAST(NULL AS UNIQUEIDENTIFIER) AS [RuleId],
        CAST(NULL AS NVARCHAR(256)) AS [RuleName]
    FROM
        (
            SELECT DISTINCT
                [CipherId]
            FROM
                [dbo].[AccessAuditEvent]
            WHERE
                [OrganizationId] = @OrganizationId
                AND [OccurredDate] >= @StartDate
                AND [OccurredDate] <= @EndDate
                AND [CipherId] IS NOT NULL
        ) C
    CROSS APPLY
        (
            SELECT TOP 1
                E.[CollectionId]
            FROM
                [dbo].[AccessAuditEvent] E
            WHERE
                E.[OrganizationId] = @OrganizationId
                AND E.[CipherId] = C.[CipherId]
                AND E.[OccurredDate] >= @StartDate
                AND E.[OccurredDate] <= @EndDate
            ORDER BY
                E.[OccurredDate] DESC,
                E.[Id] DESC
        ) L

    UNION ALL

    SELECT
        NULL,
        NULL,
        R.[AccessRuleId],
        L.[RuleName]
    FROM
        (
            SELECT DISTINCT
                [AccessRuleId]
            FROM
                [dbo].[AccessAuditEvent]
            WHERE
                [OrganizationId] = @OrganizationId
                AND [OccurredDate] >= @StartDate
                AND [OccurredDate] <= @EndDate
                AND [AccessRuleId] IS NOT NULL
        ) R
    CROSS APPLY
        (
            SELECT TOP 1
                E.[RuleName]
            FROM
                [dbo].[AccessAuditEvent] E
            WHERE
                E.[OrganizationId] = @OrganizationId
                AND E.[AccessRuleId] = R.[AccessRuleId]
                AND E.[OccurredDate] >= @StartDate
                AND E.[OccurredDate] <= @EndDate
            ORDER BY
                E.[OccurredDate] DESC,
                E.[Id] DESC
        ) L
END
GO
