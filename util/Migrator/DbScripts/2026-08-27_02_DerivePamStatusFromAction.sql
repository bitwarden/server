-- Derive PAM request and lease status from stored facts at read time.
--
-- The database stores facts -- what a party did to a record, and when; what a record means right now (its status)
-- is an interpretation of those facts against the clock, computed at read time. "Pending" and "Active" become the
-- absence of a recorded fact, and "Expired" (the clock's verdict) becomes unrepresentable in storage, so nothing
-- stored can go stale. Finishes what PM-42355 started on the lease side and removes the stored claim it left behind.
--
-- Schema: metadata-only renames, no data migration -- bytes and values are unchanged.
--   AccessRequest: [Status] -> [Action] (0 None was Pending; Expired, byte 4, is retired -- it was never stored),
--                  [ResolvedDate] -> [ActionDate], and the three Status-suffixed indexes.
--   AccessLease:   [Status] -> [Action] (0 None was Active; the stored Expired byte 1 is retired -- nothing wrote
--                  it), and the four Status-suffixed indexes (rename only, keep shapes; the CipherId one anchors the
--                  singleton guard's range lock).
--
-- Procedures: every one touching the renamed columns is re-issued below. Reads return stored facts only -- the
-- ProducedLeaseStatus CASE projections come out (the repositories compute derived statuses against the caller's
-- clock); WHERE clauses carry plain clock comparisons where filtering requires them. Write guards tighten:
-- AccessRequest_Cancel / AccessRequest_CancelWithDecision refuse a lapsed window, so a row users saw as derived
-- Expired can no longer restamp to Cancelled/Denied.

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
-- The pending-inbox index is the one exception to rename-only: it is renamed *and* reshaped (PM-42655). The read it
-- serves now filters [Action] = 0 AND [NotAfter] > @Now, and under derived status nothing ever writes Expired, so
-- the lapsed unanswered rows that second predicate discards sit at [Action] = 0 and grow without bound. Rename
-- first, then rebuild in place with DROP_EXISTING so the third key column lands in one pass.
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

-- New covering indexes for the two history reads (PM-42655). Both filter with an OR that spans [Action] and
-- [NotAfter] -- non-sargable, so no index can seek it -- which leaves the retention bound as the only predicate
-- that can keep the result set small. Lead with [CreationDate] so it does, and INCLUDE the two columns the OR
-- needs so settling it costs no lookup.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessRequest_CollectionId_CreationDate' AND [object_id] = OBJECT_ID('[dbo].[AccessRequest]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessRequest_CollectionId_CreationDate]
        ON [dbo].[AccessRequest] ([CollectionId] ASC, [CreationDate] ASC)
        INCLUDE ([Action], [NotAfter]);
END
GO
-- The requester side additionally pages TOP (250) ORDER BY [CreationDate] DESC;
-- [IX_AccessRequest_RequesterId_CipherId_Action] cannot order that ([CipherId] intervenes), so today it sorts the
-- requester's whole history to return one page. This index turns that into a backward ordered scan.
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
    -- XACT_ABORT rolls the transaction back as a unit if either write fails. Without it a constraint violation aborts
    -- only the offending statement, execution falls through to the COMMIT, and the request would be persisted without
    -- the decision that approved it.
    SET XACT_ABORT ON

    -- Atomically record an auto-approved request and its automatic verdict. No lease is minted here: the requester
    -- activates the approved request later via [AccessLease_CreateFromApprovedRequest], exactly like the human path
    -- after approval. The per-cipher single-active-lease guard therefore lives entirely on that activation path.
    BEGIN TRANSACTION AccessRequest_CreateAutoApproved

    -- The request is created with its approval already recorded. ExtensionOfLeaseId stays NULL: it is reserved for extension
    -- requests; provenance for an original lease flows the other way, via AccessLease.AccessRequestId.
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
    -- An explicit transaction holds the per-lease range lock until the writes commit, so concurrent extensions of
    -- the same lease serialize. XACT_ABORT guarantees rollback (and a clean pooled connection) on any error.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- Lock the parent lease row for the life of the transaction. A second concurrent extension of the same lease
    -- blocks here until this transaction commits, then re-counts below and sees this extension. The lease must
    -- have no early end recorded and be in-window to be extendable; outcome 0 is distinct from the cap conflict (-1).
    IF NOT EXISTS (
        SELECT 1
        FROM [dbo].[AccessLease] WITH (UPDLOCK, HOLDLOCK)
        WHERE [Id] = @ExtensionOfLeaseId
            AND [RequesterId] = @RequesterId
            AND [Action] = 0 /* None (no early end) */
            AND [NotAfter] > @Now
    )
    BEGIN
        -- There is nothing left to extend, but the attempt is still an answerable request: record it denied, with
        -- an automatic verdict naming why, so the requester can find it instead of getting only a failed call
        -- (PM-42632). The window stored is the one that was asked for -- it was never applied to the lease, which
        -- is left untouched.
        --
        -- This row carries ExtensionOfLeaseId, so it counts toward the cap re-checked below and by
        -- [AccessRequest_CountExtensionsByLeaseId]. That costs nothing: a lease only reaches this branch once it is
        -- permanently un-extendable, so there is no later extension for it to consume.
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

    -- A lease may be extended exactly once. Counted under the lease lock, so it is race-safe against a concurrent
    -- extension of the same lease.
    IF EXISTS (SELECT 1 FROM [dbo].[AccessRequest] WHERE [ExtensionOfLeaseId] = @ExtensionOfLeaseId)
    BEGIN
        ROLLBACK TRANSACTION
        SELECT -1 -- AlreadyExtended
        RETURN
    END

    -- Record the auto-approved extension request and its automatic verdict, then push the parent lease's end out in
    -- place. No new lease is minted — extending reuses the existing lease, preserving the single-active-lease
    -- invariant. The request's window spans the extension ([old lease end] .. [new lease end]); its NotAfter is the
    -- lease's new end.
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
    -- XACT_ABORT rolls the transaction back as a unit if either write fails. Without it a constraint violation aborts
    -- only the offending statement, execution falls through to the COMMIT, and the other half is persisted alone.
    SET XACT_ABORT ON

    -- Atomically record the human approver's action on an open request, together with their decision. The caller has
    -- already verified (and the application enforces) that no action is recorded yet; the WHERE guard keeps the write
    -- idempotent under a race so a second approver can't move an already-resolved request -- the column CAS decides
    -- who gets to write history, and the losing approver's verdict is never appended (@@ROWCOUNT > 0), which would
    -- leave the decision log contradicting the recorded action.
    --
    -- Approval does not mint the lease: the requester activates the approved request later via
    -- [AccessLease_CreateFromApprovedRequest]. The automatic path ([AccessRequest_CreateAutoApproved]) records the
    -- approved request the same way and likewise leaves the lease to be minted at activation.
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

    -- The requester withdraws their own not-yet-activated request (open, or an approval they have not activated).
    -- Unlike [AccessRequest_CancelWithDecision], no AccessDecision is written: a cancellation is the requester acting
    -- on their own request, not an approver verdict. The WHERE guard keeps the write idempotent under a race, refuses
    -- a request that has already produced a lease (that access is governed by the lease, which must be revoked
    -- instead), and refuses a lapsed window -- a row users saw as derived-Expired must not later restamp to
    -- Cancelled.
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
    -- XACT_ABORT rolls the transaction back as a unit if either write fails. Without it a constraint violation aborts
    -- only the offending statement, execution falls through to the COMMIT, and the other half is persisted alone.
    SET XACT_ABORT ON

    -- A managing approver retracts a not-yet-activated request (open, or an approval the requester has not
    -- activated): record Denied and the approver's human decision, mirroring [AccessRequest_ResolveWithDecision] but
    -- over the broader retractable set. The WHERE guard is race-safe, refuses a request that has produced a lease
    -- (governed by the lease -- revoke instead), and refuses a lapsed window -- a row users saw as derived-Expired
    -- must not later restamp to Denied. The decision is inserted only when the transition actually happened
    -- (@@ROWCOUNT > 0), so a no-op never orphans an AccessDecision.
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

    -- @Now is accepted and unused: callers pass the read clock (older servers unconditionally), but nothing here
    -- consults it any more -- only stored facts leave this read, and the derived statuses are computed against the
    -- caller's clock at the repository boundary.

    -- A single access request projected for the dedicated request page, returned as two result sets so the caller can
    -- attach the request's full decision list without an N+1:
    --   1) the request row with the denormalized requester identity. A row that produced a lease carries the lease's
    --      id and raw columns so the client can show (and gate) lease actions; a request produces at most one lease
    --      ([IX_AccessLease_AccessRequestId] is unique), so that join adds at most one row.
    --   2) every decision (human or automatic) for the request, keyed by AccessRequestId and ordered oldest-first;
    --      DeciderKind says which, and a human decision's identity is denormalized from [User].
    -- Only stored facts leave this read: the derived request status and the produced lease's derived status are
    -- computed at the repository boundary against the caller's clock, off [Action]/[NotAfter] and the lease's own
    -- [Action]/[NotAfter] (an extension pushes the lease's end out in place, so the request's [NotAfter] would report
    -- a live lease as expired).
    -- Authorization (requester or managing approver) is enforced by the caller, not this read.
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

    -- @Now and @Since default so a rolling deployment stays safe: an older server that predates these parameters
    -- calls the procedure without them, and gets the database clock for @Now plus a NULL @Since, which means no
    -- window -- exactly the behaviour it was written against.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- The caller's own requests, returned as two result sets so the caller can attach each request's decision list
    -- without an N+1:
    --   1) the caller's requests (TOP 250 most recent). Unlike the approver-inbox reads this is a caller-scoped
    --      self-read, so the cipher/collection/requester display-name joins are intentionally omitted (those names
    --      come from the caller's local vault, and the requester is the caller).
    --   2) every decision (human or automatic) on those requests, keyed by AccessRequestId and ordered oldest-first;
    --      DeciderKind says which, and a human decision's identity is denormalized from [User] -- the requester has no
    --      other way to name who decided their request.
    --
    -- @Since holds history rows to the same retention window the approver-side history reads use, so the same
    -- resolved request does not outlive itself on one surface and vanish from the other (PM-42614). Live rows are
    -- exempt: an open request ([Action] 0) with an unlapsed window is still answerable, and an approved one with an
    -- unlapsed window can still be activated, so neither is history and neither ages out. A lapsed unanswered row
    -- needs no exemption -- it is derived Expired, which is history, and it ages out with the rest.
    --
    -- The page of ids is materialized first so both result sets are bounded by the same 250 rows. Selecting decisions
    -- straight from [RequesterId] would return the caller's entire decision history for the caller to then discard
    -- everything outside the page.
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

    -- A request produces at most one lease ([IX_AccessLease_AccessRequestId] is unique), so this joins at most one
    -- row. Only stored facts leave this read: derived statuses are computed at the repository boundary -- see
    -- AccessRequest_ReadDetailsById for why the lease's own [Action]/[NotAfter] are returned for that.
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

    -- @Now defaults so a rolling deployment stays safe: an older server that predates this parameter calls the
    -- procedure without it and gets the database clock, which filters the same way.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- The approver inbox: actionable requests for the supplied (caller-manageable) collections, joined with the
    -- denormalized requester identity the client needs so it avoids an N+1. Actionable means no action recorded AND a
    -- window still open -- a lapsed unanswered row is derived Expired, leaves this inbox, and lands in the history
    -- read instead. An open request has not been decided by anyone yet, so it carries no approvers (the caller leaves
    -- the request's approvers list empty); only the resolved reads return a second decision result set. No AccessLease
    -- join: a lease is only ever minted from an approved request, so an open row cannot have one -- the produced-lease
    -- columns are simply absent and hydrate as no-lease (the EF read skips the same lookup for the same reason).
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

    -- @Now defaults so a rolling deployment stays safe: an older server that predates this parameter calls the
    -- procedure without it and gets the database clock, which filters the same way.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- The approver history, returned as two result sets so the caller can attach each request's full decision list
    -- without an N+1:
    --   1) the non-actionable requests -- an action recorded, or a window lapsed with none (derived Expired); the
    --      exact complement of the pending inbox read -- created on or after @Since, for the supplied
    --      (caller-manageable) collections, with the denormalized requester identity. Rows that produced a lease
    --      carry the lease's id and raw columns so the client can target (and gate) the Revoke action; a request
    --      produces at most one lease ([IX_AccessLease_AccessRequestId] is unique), so that join adds at most one
    --      row. Derived statuses are computed at the repository boundary -- see AccessRequest_ReadDetailsById.
    --   2) every decision (human or automatic) for those requests, keyed by AccessRequestId and ordered oldest-first;
    --      DeciderKind says which, and a human decision's identity is denormalized from [User].
    --
    -- The qualifying ids are materialized once so the history predicate is written once and both result sets are
    -- bounded by exactly the same rows -- the request list and its decision list cannot drift.
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

    -- @Now defaults so a rolling deployment stays safe: an older server that predates this parameter calls the
    -- procedure without it and gets the database clock, which filters the same way.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- The caller's open request for the cipher whose window can still be answered. A lapsed unanswered request is
    -- derived Expired: excluding it here un-blocks resubmission (SubmitAccessRequestCommand's duplicate guard) and
    -- keeps the client from showing a dead pending banner.
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

    -- The caller's approved-but-not-yet-activated request whose window can still produce access. Future windows are
    -- included (the client shows the upcoming window); lapsed windows are excluded so the client never offers an
    -- activation that the server would reject. A request that has produced a lease is activated, not approved.
    -- Extension requests are excluded: an approved extension pushes its parent lease's end out in place and never
    -- produces a lease of its own, so it must not surface here as an activatable "Start access" request.
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
    -- An explicit transaction is required so the singleton guard's range lock is held until the INSERT commits;
    -- XACT_ABORT guarantees the transaction is rolled back (and the pooled connection left clean) if the
    -- unique-index backstop [IX_AccessLease_AccessRequestId] trips on a concurrent activation of the same request.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- Per-cipher singleton guard. When the governing rule(s) ask for a single active lease, activation is allowed
    -- only if no other in-window lease with no early end exists for the same cipher across all users. The UPDLOCK, HOLDLOCK
    -- range lock is held for the life of this transaction, so it serializes against the INSERT below: a concurrent
    -- same-cipher activation blocks here until this transaction commits, then sees the new lease and is rejected.
    -- Outcome -1 is distinct from the precondition-fail outcome (0) so the caller can surface a 409 conflict.
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

    -- Activation of an approved request: mints the lease that authorizes access, spanning the request's
    -- approved window. Every application-level precondition is re-checked inside the INSERT so a concurrent
    -- activation cannot double-mint; zero rows inserted means a precondition no longer held and the caller decides
    -- how to surface that. [IX_AccessLease_AccessRequestId] (unique) is the backstop when two calls pass the
    -- NOT EXISTS check simultaneously.
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
    -- XACT_ABORT rolls the transaction back as a unit if either write fails. Without it a constraint violation aborts
    -- only the offending statement, execution falls through to the COMMIT, and the other half is persisted alone.
    SET XACT_ABORT ON

    -- Atomically end a running lease and capture who/why. @Action is the early end being recorded: 2 (Revoked) when
    -- an operator ended it, 3 (Cancelled) when the holder ended their own; RevokedDate/RevokedBy record when/who
    -- either way. The reason has no dedicated column, so it is preserved as a human AccessDecision (Deny) against the
    -- lease's originating request, keeping the audit trail without a schema change. The WHERE guard keeps the end
    -- idempotent if two callers race.
    --
    -- OUTPUT captures the ended lease's own AccessRequestId, which does double duty: the decision is written only when
    -- the transition actually happened (a repeat revoke ends nothing and appends nothing), and it is written against
    -- the request that lease actually came from rather than one supplied by the caller.
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

    -- Governance view: every currently-active lease (no early end recorded, window containing @Now) on the supplied
    -- (caller-manageable) collections, across all members -- not just the caller's own.
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

    -- @Now defaults so a rolling deployment stays safe: an older server that predates this parameter calls the
    -- procedure without it and gets the database clock, which filters the same way.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- Governance history: leases that have ended (derived Expired, or ended early) on the supplied
    -- (caller-manageable) collections, that ended on or after @Since. An ended-early lease's end is its RevokedDate;
    -- an expired lease's end is its NotAfter. Most recently ended first.
    --
    -- "Ended" has to be derived, not read: [Action] only ever records an early end, so a lease whose window simply
    -- closed carries 0 (None) forever, and only the clock can call it Expired. Only stored facts leave this read;
    -- the derived status is computed at the repository boundary from the same columns.
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
        -- Window closed on its own: its end is NotAfter. Byte 1 (the retired stored Expired) is deliberately NOT
        -- matched: nothing ever wrote it, and ComputeLeaseStatus has no arm for it, so reading such a stray row
        -- would fail the whole endpoint. Not read means not derived -- it simply stays invisible.
        OR (L.[Action] = 0 AND L.[NotAfter] <= @Now AND L.[NotAfter] >= @Since)
    ORDER BY
        CASE WHEN L.[Action] IN (2, 3) THEN L.[RevokedDate] ELSE L.[NotAfter] END DESC
END
GO
