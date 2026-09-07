-- Derives PAM status from stored facts; renames [Status]->[Action], [ResolvedDate]->[ActionDate].
-- AccessRequest column renames (preserve position, NULL-ness, and data)
IF COL_LENGTH('[dbo].[AccessRequest]', 'Status') IS NOT NULL
    AND COL_LENGTH('[dbo].[AccessRequest]', 'Action') IS NULL
BEGIN
    EXEC sp_rename '[dbo].[AccessRequest].[Status]', 'Action', 'COLUMN';
END
GO

IF COL_LENGTH('[dbo].[AccessRequest]', 'ResolvedDate') IS NOT NULL
    AND COL_LENGTH('[dbo].[AccessRequest]', 'ActionDate') IS NULL
BEGIN
    EXEC sp_rename '[dbo].[AccessRequest].[ResolvedDate]', 'ActionDate', 'COLUMN';
END
GO

-- AccessLease column rename
IF COL_LENGTH('[dbo].[AccessLease]', 'Status') IS NOT NULL
    AND COL_LENGTH('[dbo].[AccessLease]', 'Action') IS NULL
BEGIN
    EXEC sp_rename '[dbo].[AccessLease].[Status]', 'Action', 'COLUMN';
END
GO

-- Index renames (rename only, shapes unchanged -- except the pending-inbox index, reshaped below)
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessRequest_RequesterId_CipherId_Status' AND [object_id] = OBJECT_ID('[dbo].[AccessRequest]'))
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessRequest_RequesterId_CipherId_Action' AND [object_id] = OBJECT_ID('[dbo].[AccessRequest]'))
BEGIN
    EXEC sp_rename '[dbo].[AccessRequest].[IX_AccessRequest_RequesterId_CipherId_Status]', 'IX_AccessRequest_RequesterId_CipherId_Action', 'INDEX';
END
GO
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessRequest_OrganizationId_Status' AND [object_id] = OBJECT_ID('[dbo].[AccessRequest]'))
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessRequest_OrganizationId_Action' AND [object_id] = OBJECT_ID('[dbo].[AccessRequest]'))
BEGIN
    EXEC sp_rename '[dbo].[AccessRequest].[IX_AccessRequest_OrganizationId_Status]', 'IX_AccessRequest_OrganizationId_Action', 'INDEX';
END
GO
-- Renamed/reshaped: a lapsed unanswered row stays [Action] = 0, so reads also filter [NotAfter].
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessRequest_CollectionId_Status' AND [object_id] = OBJECT_ID('[dbo].[AccessRequest]'))
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessRequest_CollectionId_Action_NotAfter' AND [object_id] = OBJECT_ID('[dbo].[AccessRequest]'))
BEGIN
    EXEC sp_rename '[dbo].[AccessRequest].[IX_AccessRequest_CollectionId_Status]', 'IX_AccessRequest_CollectionId_Action_NotAfter', 'INDEX';
END
GO
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessRequest_CollectionId_Action_NotAfter' AND [object_id] = OBJECT_ID('[dbo].[AccessRequest]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessRequest_CollectionId_Action_NotAfter]
        ON [dbo].[AccessRequest] ([CollectionId] ASC, [Action] ASC, [NotAfter] ASC)
        WITH (DROP_EXISTING = ON);
END
ELSE
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessRequest_CollectionId_Action_NotAfter]
        ON [dbo].[AccessRequest] ([CollectionId] ASC, [Action] ASC, [NotAfter] ASC);
END
GO

-- Covers two history reads; OR on [Action]/[NotAfter] isn't sargable, so [CreationDate] leads.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessRequest_CollectionId_CreationDate' AND [object_id] = OBJECT_ID('[dbo].[AccessRequest]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessRequest_CollectionId_CreationDate]
        ON [dbo].[AccessRequest] ([CollectionId] ASC, [CreationDate] ASC)
        INCLUDE ([Action], [NotAfter]);
END
GO
-- Backs the requester's TOP(250) history page; the existing index can't order by CreationDate here.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessRequest_RequesterId_CreationDate' AND [object_id] = OBJECT_ID('[dbo].[AccessRequest]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessRequest_RequesterId_CreationDate]
        ON [dbo].[AccessRequest] ([RequesterId] ASC, [CreationDate] ASC)
        INCLUDE ([Action], [NotAfter]);
END
GO
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessLease_RequesterId_CipherId_Status' AND [object_id] = OBJECT_ID('[dbo].[AccessLease]'))
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessLease_RequesterId_CipherId_Action' AND [object_id] = OBJECT_ID('[dbo].[AccessLease]'))
BEGIN
    EXEC sp_rename '[dbo].[AccessLease].[IX_AccessLease_RequesterId_CipherId_Status]', 'IX_AccessLease_RequesterId_CipherId_Action', 'INDEX';
END
GO
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessLease_NotAfter_Status' AND [object_id] = OBJECT_ID('[dbo].[AccessLease]'))
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessLease_NotAfter_Action' AND [object_id] = OBJECT_ID('[dbo].[AccessLease]'))
BEGIN
    EXEC sp_rename '[dbo].[AccessLease].[IX_AccessLease_NotAfter_Status]', 'IX_AccessLease_NotAfter_Action', 'INDEX';
END
GO
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessLease_CollectionId_Status' AND [object_id] = OBJECT_ID('[dbo].[AccessLease]'))
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessLease_CollectionId_Action' AND [object_id] = OBJECT_ID('[dbo].[AccessLease]'))
BEGIN
    EXEC sp_rename '[dbo].[AccessLease].[IX_AccessLease_CollectionId_Status]', 'IX_AccessLease_CollectionId_Action', 'INDEX';
END
GO
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessLease_CipherId_Status' AND [object_id] = OBJECT_ID('[dbo].[AccessLease]'))
    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessLease_CipherId_Action' AND [object_id] = OBJECT_ID('[dbo].[AccessLease]'))
BEGIN
    EXEC sp_rename '[dbo].[AccessLease].[IX_AccessLease_CipherId_Status]', 'IX_AccessLease_CipherId_Action', 'INDEX';
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @ExtensionOfLeaseId UNIQUEIDENTIFIER = NULL,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @RequesterId UNIQUEIDENTIFIER,
    @NotBefore DATETIME2(7),
    @NotAfter DATETIME2(7),
    @Reason NVARCHAR(MAX) = NULL,
    @Action TINYINT,
    @CreationDate DATETIME2(7),
    @ActionDate DATETIME2(7) = NULL,
    @RuleId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[AccessRequest]
    (
        [Id],
        [ExtensionOfLeaseId],
        [OrganizationId],
        [CollectionId],
        [CipherId],
        [RequesterId],
        [NotBefore],
        [NotAfter],
        [Reason],
        [Action],
        [CreationDate],
        [ActionDate],
        [RuleId]
    )
    VALUES
    (
        @Id,
        @ExtensionOfLeaseId,
        @OrganizationId,
        @CollectionId,
        @CipherId,
        @RequesterId,
        @NotBefore,
        @NotAfter,
        @Reason,
        @Action,
        @CreationDate,
        @ActionDate,
        @RuleId
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_CreateAutoApproved]
    @AccessRequestId UNIQUEIDENTIFIER,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @RequesterId UNIQUEIDENTIFIER,
    @NotBefore DATETIME2(7),
    @NotAfter DATETIME2(7),
    @Reason NVARCHAR(MAX) = NULL,
    @ConditionKind TINYINT = NULL,
    @CreationDate DATETIME2(7),
    @RuleId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON
    -- Both writes commit or roll back together (XACT_ABORT).
    SET XACT_ABORT ON

    -- No lease minted here; requester activates the approved request later, human-path style.
    BEGIN TRANSACTION AccessRequest_CreateAutoApproved

    -- ExtensionOfLeaseId stays NULL here; it's reserved for extension requests only.
    INSERT INTO [dbo].[AccessRequest]
    (
        [Id], [ExtensionOfLeaseId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [NotBefore], [NotAfter], [Reason], [Action], [CreationDate], [ActionDate], [RuleId]
    )
    VALUES
    (
        @AccessRequestId, NULL, @OrganizationId, @CollectionId, @CipherId, @RequesterId,
        @NotBefore, @NotAfter, @Reason, 1 /* Approved */, @CreationDate, @CreationDate, @RuleId
    )

    INSERT INTO [dbo].[AccessDecision]
    (
        [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
        [Verdict], [Comment], [EvaluationContext], [CreationDate]
    )
    VALUES
    (
        @AccessDecisionId, @AccessRequestId, 0 /* Automatic */, NULL, @ConditionKind,
        1 /* Approve */, NULL, NULL, @CreationDate
    )

    COMMIT TRANSACTION AccessRequest_CreateAutoApproved
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
    @DenialComment NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON
    -- The transaction holds the per-lease range lock until commit, serializing concurrent extensions.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- Locks the parent lease for the transaction so a concurrent extension serializes on it.
    IF NOT EXISTS (
        SELECT 1
        FROM [dbo].[AccessLease] WITH (UPDLOCK, HOLDLOCK)
        WHERE [Id] = @ExtensionOfLeaseId
            AND [RequesterId] = @RequesterId
            AND [Action] = 0 /* None (no early end) */
            AND [NotAfter] > @Now
    )
    BEGIN
        -- Recorded as an answerable denied request, not a failed call; counts toward the cap.
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

    -- At most one extension per lease; counted under the lease lock, so it's race-safe.
    IF EXISTS (SELECT 1 FROM [dbo].[AccessRequest] WHERE [ExtensionOfLeaseId] = @ExtensionOfLeaseId)
    BEGIN
        ROLLBACK TRANSACTION
        SELECT -1 -- AlreadyExtended
        RETURN
    END

    -- No new lease is minted: extending reuses the existing lease, preserving the single-active-lease invariant.
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
        @AccessDecisionId, @AccessRequestId, 0 /* Automatic */, NULL, NULL,
        1 /* Approve */, NULL, NULL, @Now
    )

    UPDATE [dbo].[AccessLease]
    SET [NotAfter] = @NotAfter
    WHERE [Id] = @ExtensionOfLeaseId

    COMMIT TRANSACTION

    SELECT 1 -- Extended
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ResolveWithDecision]
    @AccessRequestId UNIQUEIDENTIFIER,
    @Action TINYINT,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @ApproverId UNIQUEIDENTIFIER,
    @Verdict TINYINT,
    @Comment NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- Both writes commit or roll back together (XACT_ABORT).
    SET XACT_ABORT ON

    -- Records the approver's decision atomically; the WHERE guard is a CAS for first-verdict-wins.
    BEGIN TRANSACTION AccessRequest_Resolve

    UPDATE [dbo].[AccessRequest]
    SET [Action] = @Action,
        [ActionDate] = @Now
    WHERE [Id] = @AccessRequestId AND [Action] = 0 -- None (open)

    IF @@ROWCOUNT > 0
    BEGIN
        INSERT INTO [dbo].[AccessDecision]
        (
            [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
            [Verdict], [Comment], [EvaluationContext], [CreationDate]
        )
        VALUES
        (
            @AccessDecisionId, @AccessRequestId, 1 /* Human */, @ApproverId, NULL,
            @Verdict, @Comment, NULL, @Now
        )
    END

    COMMIT TRANSACTION AccessRequest_Resolve
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_Cancel]
    @AccessRequestId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Requester withdrawal of a not-yet-activated request; no AccessDecision, since it isn't an approver verdict.
    UPDATE [dbo].[AccessRequest]
    SET [Action] = 3, -- Cancelled
        [ActionDate] = @Now
    WHERE [Id] = @AccessRequestId
        AND [Action] IN (0, 1) -- None (open) or Approved
        AND [NotAfter] > @Now
        AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] L WHERE L.[AccessRequestId] = @AccessRequestId)
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_CancelWithDecision]
    @AccessRequestId UNIQUEIDENTIFIER,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @ApproverId UNIQUEIDENTIFIER,
    @Verdict TINYINT,
    @Comment NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- Both writes commit or roll back together (XACT_ABORT).
    SET XACT_ABORT ON

    -- Approver retraction of a not-yet-activated request; AccessDecision is inserted only on an actual transition.
    BEGIN TRANSACTION AccessRequest_CancelWithDecision

    UPDATE [dbo].[AccessRequest]
    SET [Action] = 2, -- Denied
        [ActionDate] = @Now
    WHERE [Id] = @AccessRequestId
        AND [Action] IN (0, 1) -- None (open) or Approved
        AND [NotAfter] > @Now
        AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] L WHERE L.[AccessRequestId] = @AccessRequestId)

    IF @@ROWCOUNT > 0
    BEGIN
        INSERT INTO [dbo].[AccessDecision]
        (
            [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
            [Verdict], [Comment], [EvaluationContext], [CreationDate]
        )
        VALUES
        (
            @AccessDecisionId, @AccessRequestId, 1 /* Human */, @ApproverId, NULL,
            @Verdict, @Comment, NULL, @Now
        )
    END

    COMMIT TRANSACTION AccessRequest_CancelWithDecision
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadDetailsById]
    @Id UNIQUEIDENTIFIER,
    @Now DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- @Now is accepted but unused, kept for older callers that still pass it.
    -- Only stored facts leave this read; derived status/authorization are handled by the caller.
    SELECT
        LR.[Id],
        LR.[ExtensionOfLeaseId],
        LR.[OrganizationId],
        LR.[CollectionId],
        LR.[CipherId],
        LR.[RequesterId],
        LR.[NotBefore],
        LR.[NotAfter],
        LR.[Reason],
        LR.[Action],
        LR.[CreationDate],
        LR.[ActionDate],
        LR.[RuleId],
        PL.[Id] AS [ProducedLeaseId],
        PL.[Action] AS [ProducedLeaseAction],
        PL.[NotAfter] AS [ProducedLeaseNotAfter],
        U.[Name] AS [RequesterName],
        U.[Email] AS [RequesterEmail]
    FROM [dbo].[AccessRequest] LR
    LEFT JOIN [dbo].[User] U ON U.[Id] = LR.[RequesterId]
    LEFT JOIN [dbo].[AccessLease] PL ON PL.[AccessRequestId] = LR.[Id]
    WHERE LR.[Id] = @Id

    SELECT
        AD.[AccessRequestId],
        AD.[DeciderKind] AS [DeciderKind],
        AD.[ApproverId] AS [Id],
        AU.[Name] AS [Name],
        AU.[Email] AS [Email],
        AD.[Comment] AS [Comment],
        AD.[Verdict] AS [Verdict],
        AD.[CreationDate] AS [DecidedAt]
    FROM [dbo].[AccessDecision] AD
    LEFT JOIN [dbo].[User] AU ON AU.[Id] = AD.[ApproverId]
    WHERE AD.[AccessRequestId] = @Id
    ORDER BY AD.[CreationDate] ASC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadManyByRequesterId]
    @RequesterId UNIQUEIDENTIFIER,
    @Now DATETIME2(7) = NULL,
    @Since DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Lets older callers omit @Now and @Since during rolling deployment.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- @Since matches the approver-side retention window; live rows are exempt from it.
    DECLARE @RequestIds TABLE ([Id] UNIQUEIDENTIFIER PRIMARY KEY)

    INSERT INTO @RequestIds ([Id])
    SELECT TOP (250) [Id]
    FROM [dbo].[AccessRequest]
    WHERE [RequesterId] = @RequesterId
        AND (
            @Since IS NULL
            OR [CreationDate] >= @Since
            OR ([Action] IN (0, 1) AND [NotAfter] > @Now) -- live: open and answerable, or approved and activatable
        )
    ORDER BY [CreationDate] DESC

    -- A request produces at most one lease, so this joins at most one row.
    SELECT
        LR.[Id],
        LR.[ExtensionOfLeaseId],
        LR.[OrganizationId],
        LR.[CollectionId],
        LR.[CipherId],
        LR.[RequesterId],
        LR.[NotBefore],
        LR.[NotAfter],
        LR.[Reason],
        LR.[Action],
        LR.[CreationDate],
        LR.[ActionDate],
        LR.[RuleId],
        PL.[Id] AS [ProducedLeaseId],
        PL.[Action] AS [ProducedLeaseAction],
        PL.[NotAfter] AS [ProducedLeaseNotAfter]
    FROM [dbo].[AccessRequest] LR
    INNER JOIN @RequestIds RI ON RI.[Id] = LR.[Id]
    LEFT JOIN [dbo].[AccessLease] PL ON PL.[AccessRequestId] = LR.[Id]
    ORDER BY LR.[CreationDate] DESC

    SELECT
        AD.[AccessRequestId],
        AD.[DeciderKind] AS [DeciderKind],
        AD.[ApproverId] AS [Id],
        AU.[Name] AS [Name],
        AU.[Email] AS [Email],
        AD.[Comment] AS [Comment],
        AD.[Verdict] AS [Verdict],
        AD.[CreationDate] AS [DecidedAt]
    FROM [dbo].[AccessDecision] AD
    INNER JOIN @RequestIds RI ON RI.[Id] = AD.[AccessRequestId]
    LEFT JOIN [dbo].[User] AU ON AU.[Id] = AD.[ApproverId]
    ORDER BY AD.[AccessRequestId], AD.[CreationDate] ASC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadInboxPendingByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Now DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Lets older callers omit @Now during rolling deployment.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- Actionable rows have no action recorded and an open window; lapsed rows derive Expired.
    SELECT
        LR.[Id],
        LR.[ExtensionOfLeaseId],
        LR.[OrganizationId],
        LR.[CollectionId],
        LR.[CipherId],
        LR.[RequesterId],
        LR.[NotBefore],
        LR.[NotAfter],
        LR.[Reason],
        LR.[Action],
        LR.[CreationDate],
        LR.[ActionDate],
        LR.[RuleId],
        U.[Name] AS [RequesterName],
        U.[Email] AS [RequesterEmail]
    FROM [dbo].[AccessRequest] LR
    INNER JOIN @CollectionIds CI ON CI.[Id] = LR.[CollectionId]
    LEFT JOIN [dbo].[User] U ON U.[Id] = LR.[RequesterId]
    WHERE LR.[Action] = 0 -- None (open)
        AND LR.[NotAfter] > @Now
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadInboxHistoryByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Since DATETIME2(7),
    @Now DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Lets older callers omit @Now during rolling deployment.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- Two result sets (requests, decisions); materialized ids let both share the same rows.
    DECLARE @RequestIds TABLE ([Id] UNIQUEIDENTIFIER PRIMARY KEY)

    INSERT INTO @RequestIds ([Id])
    SELECT LR.[Id]
    FROM [dbo].[AccessRequest] LR
    INNER JOIN @CollectionIds CI ON CI.[Id] = LR.[CollectionId]
    WHERE (LR.[Action] <> 0 OR LR.[NotAfter] <= @Now) -- action recorded, or expired unanswered
        AND LR.[CreationDate] >= @Since

    SELECT
        LR.[Id],
        LR.[ExtensionOfLeaseId],
        LR.[OrganizationId],
        LR.[CollectionId],
        LR.[CipherId],
        LR.[RequesterId],
        LR.[NotBefore],
        LR.[NotAfter],
        LR.[Reason],
        LR.[Action],
        LR.[CreationDate],
        LR.[ActionDate],
        LR.[RuleId],
        PL.[Id] AS [ProducedLeaseId],
        PL.[Action] AS [ProducedLeaseAction],
        PL.[NotAfter] AS [ProducedLeaseNotAfter],
        U.[Name] AS [RequesterName],
        U.[Email] AS [RequesterEmail]
    FROM [dbo].[AccessRequest] LR
    INNER JOIN @RequestIds RI ON RI.[Id] = LR.[Id]
    LEFT JOIN [dbo].[User] U ON U.[Id] = LR.[RequesterId]
    LEFT JOIN [dbo].[AccessLease] PL ON PL.[AccessRequestId] = LR.[Id]

    SELECT
        AD.[AccessRequestId],
        AD.[DeciderKind] AS [DeciderKind],
        AD.[ApproverId] AS [Id],
        AU.[Name] AS [Name],
        AU.[Email] AS [Email],
        AD.[Comment] AS [Comment],
        AD.[Verdict] AS [Verdict],
        AD.[CreationDate] AS [DecidedAt]
    FROM [dbo].[AccessDecision] AD
    INNER JOIN @RequestIds RI ON RI.[Id] = AD.[AccessRequestId]
    LEFT JOIN [dbo].[User] AU ON AU.[Id] = AD.[ApproverId]
    ORDER BY AD.[AccessRequestId], AD.[CreationDate] ASC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadActivePendingByRequesterIdCipherId]
    @RequesterId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @Now DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Lets older callers omit @Now during rolling deployment.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- Caller's open request for the cipher; a lapsed one derives Expired, allowing resubmission.
    SELECT TOP 1
        *
    FROM
        [dbo].[AccessRequest]
    WHERE
        [RequesterId] = @RequesterId
        AND [CipherId] = @CipherId
        AND [Action] = 0 -- None (open)
        AND [NotAfter] > @Now
    ORDER BY
        [CreationDate] DESC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadActiveApprovedByRequesterIdCipherId]
    @RequesterId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Excludes lapsed windows, already-leased requests, and extension requests (which never produce their own lease).
    SELECT TOP 1
        AR.*
    FROM
        [dbo].[AccessRequest] AR
    WHERE
        AR.[RequesterId] = @RequesterId
        AND AR.[CipherId] = @CipherId
        AND AR.[Action] = 1 -- Approved
        AND AR.[NotAfter] > @Now
        AND AR.[ExtensionOfLeaseId] IS NULL
        AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] AL WHERE AL.[AccessRequestId] = AR.[Id])
    ORDER BY
        AR.[CreationDate] DESC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_CreateFromApprovedRequest]
    @AccessLeaseId UNIQUEIDENTIFIER,
    @AccessRequestId UNIQUEIDENTIFIER,
    @RequesterId UNIQUEIDENTIFIER,
    @Now DATETIME2(7),
    @EnforceSingleActiveLease BIT = 0
AS
BEGIN
    SET NOCOUNT ON
    -- An explicit transaction holds the singleton guard's range lock until the INSERT commits.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- At most one active lease per cipher; the UPDLOCK, HOLDLOCK lock serializes concurrent activation.
    IF @EnforceSingleActiveLease = 1
        AND EXISTS (
            SELECT 1
            FROM [dbo].[AccessLease] WITH (UPDLOCK, HOLDLOCK)
            WHERE [CipherId] = (SELECT [CipherId] FROM [dbo].[AccessRequest] WHERE [Id] = @AccessRequestId)
                AND [Action] = 0 /* None (no early end) */
                AND [NotBefore] <= @Now
                AND [NotAfter] > @Now
        )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT -1
        RETURN
    END

    -- Every precondition is re-checked inside the INSERT; the unique index backstops a race.
    INSERT INTO [dbo].[AccessLease]
    (
        [Id], [AccessRequestId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [Action], [NotBefore], [NotAfter], [RevokedDate], [RevokedBy], [CreationDate]
    )
    SELECT
        @AccessLeaseId, AR.[Id], AR.[OrganizationId], AR.[CollectionId], AR.[CipherId], AR.[RequesterId],
        0 /* None (no early end) */, AR.[NotBefore], AR.[NotAfter], NULL, NULL, @Now
    FROM [dbo].[AccessRequest] AR
    WHERE
        AR.[Id] = @AccessRequestId
        AND AR.[RequesterId] = @RequesterId
        AND AR.[Action] = 1 -- Approved
        AND AR.[ExtensionOfLeaseId] IS NULL -- an extension applied in place on approval and never mints a lease
        AND AR.[NotBefore] <= @Now
        AND AR.[NotAfter] > @Now
        AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] AL WHERE AL.[AccessRequestId] = AR.[Id])

    DECLARE @Rows INT = @@ROWCOUNT

    COMMIT TRANSACTION

    -- 1 = minted, 0 = precondition no longer held (caller re-reads the winner).
    SELECT CASE WHEN @Rows = 1 THEN 1 ELSE 0 END
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_Revoke]
    @AccessLeaseId UNIQUEIDENTIFIER,
    @Action TINYINT,
    @RevokedBy UNIQUEIDENTIFIER,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @Reason NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- Both writes commit or roll back together (XACT_ABORT).
    SET XACT_ABORT ON

    -- @Action records who ended it early (2 Revoked, 3 Cancelled); AccessDecision preserves why.
    DECLARE @Ended TABLE ([AccessRequestId] UNIQUEIDENTIFIER)

    BEGIN TRANSACTION AccessLease_Revoke

    UPDATE [dbo].[AccessLease]
    SET [Action] = @Action,
        [RevokedDate] = @Now,
        [RevokedBy] = @RevokedBy
    OUTPUT INSERTED.[AccessRequestId] INTO @Ended
    WHERE [Id] = @AccessLeaseId AND [Action] = 0 -- None (no early end)

    INSERT INTO [dbo].[AccessDecision]
    (
        [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
        [Verdict], [Comment], [EvaluationContext], [CreationDate]
    )
    SELECT
        @AccessDecisionId, E.[AccessRequestId], 1 /* Human */, @RevokedBy, NULL,
        0 /* Deny */, @Reason, NULL, @Now
    FROM @Ended E

    COMMIT TRANSACTION AccessLease_Revoke
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_ReadActiveByRequesterIdCipherId]
    @RequesterId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        *
    FROM
        [dbo].[AccessLease]
    WHERE
        [RequesterId] = @RequesterId
        AND [CipherId] = @CipherId
        AND [Action] = 0 -- None (no early end)
        AND [NotBefore] <= @Now
        AND [NotAfter] > @Now
    ORDER BY
        [NotAfter] DESC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_ReadManyActiveByRequesterId]
    @RequesterId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[AccessLease]
    WHERE
        [RequesterId] = @RequesterId
        AND [Action] = 0 -- None (no early end)
        AND [NotBefore] <= @Now
        AND [NotAfter] > @Now
    ORDER BY
        [NotAfter] ASC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_ReadManyActiveByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Every member's currently-active lease (no early end, window containing @Now), not just the caller's.
    SELECT
        L.*
    FROM
        [dbo].[AccessLease] L
        INNER JOIN @CollectionIds CI ON CI.[Id] = L.[CollectionId]
    WHERE
        L.[Action] = 0 -- None (no early end)
        AND L.[NotBefore] <= @Now
        AND L.[NotAfter] > @Now
    ORDER BY
        L.[NotAfter] ASC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_ReadManyEndedByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Since DATETIME2(7),
    @Now DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Lets older callers omit @Now during rolling deployment.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- Leases ended on or after @Since; "ended" derives from [Action] recording an early end.
    SELECT
        L.[Id],
        L.[AccessRequestId],
        L.[OrganizationId],
        L.[CollectionId],
        L.[CipherId],
        L.[RequesterId],
        L.[Action],
        L.[NotBefore],
        L.[NotAfter],
        L.[RevokedDate],
        L.[RevokedBy],
        L.[CreationDate]
    FROM
        [dbo].[AccessLease] L
        INNER JOIN @CollectionIds CI ON CI.[Id] = L.[CollectionId]
    WHERE
        -- Ended early (Revoked, Cancelled): its end is RevokedDate, whatever its window says.
        (L.[Action] IN (2, 3) AND L.[RevokedDate] >= @Since)
        -- Window closed on its own (end = NotAfter); byte 1 (retired stored Expired) is never matched.
        OR (L.[Action] = 0 AND L.[NotAfter] <= @Now AND L.[NotAfter] >= @Since)
    ORDER BY
        CASE WHEN L.[Action] IN (2, 3) THEN L.[RevokedDate] ELSE L.[NotAfter] END DESC
END
GO
