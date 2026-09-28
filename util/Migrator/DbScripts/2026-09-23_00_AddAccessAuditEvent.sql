-- Add the PAM access-audit log. Consolidated net-new migration (the feature has not shipped), squashing the
-- incremental steps the store went through during development. See src/Sql/dbo/Pam/Tables/AccessAuditEvent.sql for
-- the schema rationale.
--
-- [CorrelationId] deliberately has no default, here and on the three EF providers. An action's Attempt and Outcome
-- must share one id for the read to collapse them, so the caller mints one per action; a DEFAULT NEWID() would let a
-- forgotten id become a fresh one that correlates with nothing, which reads back as a lone in-doubt half rather than
-- as an error.

IF OBJECT_ID('[dbo].[AccessAuditEvent]') IS NULL
BEGIN
    CREATE TABLE [dbo].[AccessAuditEvent] (
        [Id]                  UNIQUEIDENTIFIER    NOT NULL,
        [OrganizationId]      UNIQUEIDENTIFIER    NOT NULL,
        [Kind]                TINYINT             NOT NULL,
        [Phase]               TINYINT             NOT NULL,
        [OccurredDate]        DATETIME2(7)        NOT NULL,
        [ActorId]             UNIQUEIDENTIFIER    NULL,
        [RequesterId]         UNIQUEIDENTIFIER    NULL,
        [CollectionId]        UNIQUEIDENTIFIER    NULL,
        [CipherId]            UNIQUEIDENTIFIER    NULL,
        [AccessRequestId]     UNIQUEIDENTIFIER    NULL,
        [AccessLeaseId]       UNIQUEIDENTIFIER    NULL,
        [AccessRuleId]        UNIQUEIDENTIFIER    NULL,
        [Detail]              NVARCHAR(MAX)       NULL,
        [LeaseNotBefore]      DATETIME2(7)        NULL,
        [LeaseNotAfter]       DATETIME2(7)        NULL,
        [ActorName]           NVARCHAR(50)        NULL,
        [ActorEmail]          NVARCHAR(256)       NULL,
        [RequesterName]       NVARCHAR(50)        NULL,
        [RequesterEmail]      NVARCHAR(256)       NULL,
        [RuleName]            NVARCHAR(256)       NULL,
        [CorrelationId]       UNIQUEIDENTIFIER    NOT NULL,
        [TargetSystemId]      UNIQUEIDENTIFIER    NULL,
        [TargetSystemName]    NVARCHAR(200)       NULL,
        [AccessConnectorId]   UNIQUEIDENTIFIER    NULL,
        [AccessConnectorName] NVARCHAR(200)       NULL,
        [RotationConfigId]    UNIQUEIDENTIFIER    NULL,
        [RotationJobId]       UNIQUEIDENTIFIER    NULL,
        [RotationSource]      TINYINT             NULL,
        [SyncState]           TINYINT             NULL,
        CONSTRAINT [PK_AccessAuditEvent] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AccessAuditEvent_Organization] FOREIGN KEY ([OrganizationId])
            REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
    );
END
GO

-- For a table created by an earlier development cut, which predates the rotation columns. A no-op on a fresh one.
IF COL_LENGTH('[dbo].[AccessAuditEvent]', 'TargetSystemId') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessAuditEvent] ADD
        [TargetSystemId]      UNIQUEIDENTIFIER    NULL,
        [TargetSystemName]    NVARCHAR(200)       NULL,
        [AccessConnectorId]   UNIQUEIDENTIFIER    NULL,
        [AccessConnectorName] NVARCHAR(200)       NULL,
        [RotationConfigId]    UNIQUEIDENTIFIER    NULL,
        [RotationJobId]       UNIQUEIDENTIFIER    NULL,
        [RotationSource]      TINYINT             NULL,
        [SyncState]           TINYINT             NULL;
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessAuditEvent_OrganizationId_OccurredDate_Id' AND object_id = OBJECT_ID('[dbo].[AccessAuditEvent]'))
BEGIN
    -- An earlier development cut created this index without the INCLUDE; rebuild so it matches a fresh database.
    CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_OrganizationId_OccurredDate_Id]
        ON [dbo].[AccessAuditEvent] ([OrganizationId] ASC, [OccurredDate] DESC, [Id] DESC)
        INCLUDE ([CorrelationId], [Phase], [CipherId], [CollectionId], [AccessRuleId], [RuleName])
        WITH (DROP_EXISTING = ON);
END
ELSE
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_OrganizationId_OccurredDate_Id]
        ON [dbo].[AccessAuditEvent] ([OrganizationId] ASC, [OccurredDate] DESC, [Id] DESC)
        INCLUDE ([CorrelationId], [Phase], [CipherId], [CollectionId], [AccessRuleId], [RuleName]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessAuditEvent_CorrelationId' AND object_id = OBJECT_ID('[dbo].[AccessAuditEvent]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_CorrelationId]
        ON [dbo].[AccessAuditEvent] ([CorrelationId] ASC)
        INCLUDE ([OrganizationId], [OccurredDate], [Phase]);
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
    @SyncState TINYINT = NULL
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
        @AccessConnectorId,
        @AccessConnectorName,
        @RotationConfigId,
        @RotationJobId,
        @RotationSource,
        @SyncState
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
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessAuditEvent_ReadItemsByOrganizationId]
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
GO

-- Superseded by AccessAuditEvent_ReadPageByOrganizationId, and dropped only after that exists. The feature has not
-- shipped, so no deployed server has ever called this.
DROP PROCEDURE IF EXISTS [dbo].[AccessAuditEvent_ReadManyByOrganizationId]
GO
