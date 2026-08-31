-- Give the PAM audit log its Item filter back, now that the trail read is paged. The menu cannot come from a page
-- (fifty rows name only some of the items in range) nor from the caller's vault (which holds credentials the trail
-- never mentions), so the store answers "which subjects does the trail name in this range" directly, and the client
-- keeps the ones it can label.
--
-- Adds [dbo].[AccessAuditEvent_ReadItemsByOrganizationId], widens the trail index to cover it, and reworks the page
-- read's cipher filter into the two-column, multi-value Item selection that menu produces.

-- The four subject columns join the INCLUDE so the item read is covered rather than paying a key lookup on every row
-- of the range. They ride on this index rather than one of their own because the page read is a TOP-N seek and barely
-- notices a wider leaf row, where a second index would cost every insert.
CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_OrganizationId_OccurredAt_Id]
    ON [dbo].[AccessAuditEvent] ([OrganizationId] ASC, [OccurredAt] DESC, [Id] DESC)
    INCLUDE ([CorrelationId], [Phase], [CipherId], [CollectionId], [AccessRuleId], [RuleName])
    WITH (DROP_EXISTING = ON)
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessAuditEvent_ReadItemsByOrganizationId]
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
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessAuditEvent_ReadPageByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @StartDate DATETIME2(7),
    @EndDate DATETIME2(7),
    @PageSize INT,
    @BeforeDate DATETIME2(7) = NULL,
    @BeforeId UNIQUEIDENTIFIER = NULL,
    @Kinds NVARCHAR(MAX) = NULL,
    @ActorIds NVARCHAR(MAX) = NULL,
    @IncludeAutomatedActor BIT = 0,
    @RequesterIds NVARCHAR(MAX) = NULL,
    @CipherIds NVARCHAR(MAX) = NULL,
    @RuleIds NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Reads one page of the PAM access-audit trail for an entire organization from the append-only
    -- [AccessAuditEvent] store: the matching events between @StartDate and @EndDate, newest first, at most @PageSize
    -- of them. Fully SELF-CONTAINED -- the actor/requester/cipher/collection/rule/target-system/daemon display names
    -- were resolved and frozen into the row at write time (see AccessAuditEvent_Create), so this read touches no other
    -- table and a later delete or rename of a referenced entity cannot erase or rewrite the event. Cipher/collection
    -- names are encrypted (EncString), decrypted client-side. Org-scoped: the caller is authorized by the
    -- AccessEventLogs permission at the endpoint. Kind matches Bit.Pam.Enums.AccessAuditEventKind; Phase matches
    -- Bit.Pam.Enums.AccessAuditEventPhase; RotationSource matches Bit.Pam.Enums.PamRotationSource; SyncState matches
    -- Bit.Pam.Enums.PamRotationSyncState. Time-derived expiry kinds are not written by any action yet (deferred).
    --
    -- Replaces the unpaged AccessAuditEvent_ReadManyByOrganizationId, which returned the organization's whole
    -- retention window in one response and left the before/after collapse to the caller. Collapsing in the caller
    -- cannot survive paging -- it could not tell an Attempt whose Outcome sits on the next page from one that never
    -- landed -- so the collapse happens here, before the page is cut.
    --
    -- The list parameters carry JSON arrays ([1,13,30], ["<guid>", ...]); NULL means the dimension is unfiltered.
    -- OPENJSON rather than a table-valued parameter because Kind is a TINYINT, and only the GuidIdArray /
    -- TwoGuidIdArray / EmailArray user-defined types exist -- one mechanism for all three lists beats inventing a
    -- fourth type for one of them.
    SELECT TOP (@PageSize)
        [Id],
        [Kind],
        [Phase],
        [CorrelationId],
        [OccurredAt],
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
    FROM [dbo].[AccessAuditEvent] E
    WHERE E.[OrganizationId] = @OrganizationId
        AND E.[OccurredAt] >= @StartDate
        AND E.[OccurredAt] <= @EndDate
        -- Resume where the previous page stopped. Keyed on ([OccurredAt], [Id]) rather than [OccurredAt] alone: an
        -- action writes its before/after halves at one instant, so a boundary landing inside a group of events sharing
        -- a timestamp is ordinary here, and a date-only key would drop every row tied with it.
        AND (
            @BeforeDate IS NULL
            OR E.[OccurredAt] < @BeforeDate
            OR (E.[OccurredAt] = @BeforeDate AND E.[Id] < @BeforeId)
        )
        -- Collapse each action's before/after pair (shared CorrelationId) into one row: the Outcome when it landed,
        -- otherwise the lone Attempt -- which the response flags as in-doubt. Scoped to the same range as the page, so
        -- the collapse is a function of what the range holds; an action straddling a bound reads as in-doubt at that
        -- edge rather than disappearing from both sides of it. The [Id] arm keeps the choice deterministic if a pair
        -- ever arrives with its phase written twice.
        AND NOT EXISTS (
            SELECT 1
            FROM [dbo].[AccessAuditEvent] P
            WHERE P.[CorrelationId] = E.[CorrelationId]
                AND P.[OrganizationId] = @OrganizationId
                AND P.[OccurredAt] >= @StartDate
                AND P.[OccurredAt] <= @EndDate
                AND (
                    P.[Phase] > E.[Phase]
                    OR (P.[Phase] = E.[Phase] AND P.[Id] < E.[Id])
                )
        )
        -- The dimensions are applied AFTER the collapse, to the row that survived it, because the two halves of one
        -- action need not agree: a refused activation writes its Attempt as LeaseActivated and its Outcome as
        -- LeaseActivationRejected (ActivateAccessRequestCommand), so filtering before the collapse would answer
        -- "activated" with an action that was turned down.
        AND (
            @Kinds IS NULL
            OR E.[Kind] IN (SELECT CAST([value] AS TINYINT) FROM OPENJSON(@Kinds))
        )
        -- An actor selection unions the chosen identities with the automatic bucket, which has no id of its own.
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
        -- The Item dimension is two columns, and they UNION rather than narrow: a rule-administration event names a
        -- rule and no cipher, so one selection spanning both is asking for either, not for the empty intersection.
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
    ORDER BY E.[OccurredAt] DESC, E.[Id] DESC
END
GO
