-- Close the activation/retraction write skew (PM-41878).
--
-- Activating an approved request read [AccessRequest] and wrote only [AccessLease]; cancelling or denying one reads
-- [AccessLease] and writes only [AccessRequest]. Neither side wrote the table the other read, so both could commit
-- and leave a Cancelled/Denied request holding a live lease. Access is governed by the lease alone once it exists
-- (CipherLeaseGate hands over a gated cipher on lease existence), so that combination hands the requester the
-- credential their request was withdrawn from.
--
-- The retraction guards make it worse than a narrow window: their NOT EXISTS is correlated to @AccessRequestId
-- rather than to the row being updated, so it is an uncorrelated existence check that the optimizer may evaluate as
-- a start-up filter -- before [AccessRequest] is locked at all -- and a concurrent activation can then mint and
-- commit in the gap.
--
-- The fix makes both operations write and lock the same row, in the same order:
--
--   * [AccessLease_CreateFromApprovedRequest] claims the request row with a guarded UPDATE before it mints. The
--     write is semantically a no-op -- an activated request stays Approved, there is no 'activated' action -- but it
--     makes activation a writer of the row the retractions write, and folds every precondition into one CAS.
--     Positioned ahead of the singleton guard so both operations take [AccessRequest] before [AccessLease]; the
--     other order would deadlock (1205), which neither caller retries.
--   * [AccessRequest_Cancel] and [AccessRequest_CancelWithDecision] take that row under UPDLOCK before probing for a
--     produced lease, which takes the ordering out of the optimizer's hands. [AccessRequest_Cancel] gains an
--     explicit transaction to hold the lock across both statements; in autocommit it would be released at the end of
--     the claiming SELECT.
--
-- Neither guard's predicate changes, so no outcome that was already correct moves.

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_CreateFromApprovedRequest]
    @AccessLeaseId UNIQUEIDENTIFIER,
    @AccessRequestId UNIQUEIDENTIFIER,
    @RequesterId UNIQUEIDENTIFIER,
    @Now DATETIME2(7),
    @EnforceSingleActiveLease BIT = 0
AS
BEGIN
    SET NOCOUNT ON
    -- An explicit transaction is required so the claiming UPDATE's row lock and the singleton guard's range lock are
    -- both held until the INSERT commits; XACT_ABORT guarantees the transaction is rolled back (and the pooled
    -- connection left clean) if the unique-index backstop [IX_AccessLease_AccessRequestId] trips on a concurrent
    -- activation of the same request.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- Claim the request row, then mint. Activation used to read [AccessRequest] and write only [AccessLease] while a
    -- retraction reads [AccessLease] and writes only [AccessRequest] -- write skew across two tables, where neither
    -- side writes what the other reads, so both could commit and leave a Cancelled/Denied request holding a live
    -- lease. Access is governed by the lease alone once it exists, so that combination hands the requester the
    -- credential their request was withdrawn from.
    --
    -- This UPDATE closes it from the activation side. It is semantically a no-op -- an activated request stays
    -- Approved, there is no 'activated' action -- but it makes activation a *writer* of the row the retraction paths
    -- write, so the two serialize on that row's exclusive lock, which is held until this transaction commits. The
    -- other half is in [AccessRequest_Cancel] and [AccessRequest_CancelWithDecision], which take the same row under
    -- UPDLOCK before they probe for a lease.
    --
    -- Every application-level precondition is re-checked here rather than only in the INSERT below, so the claim and
    -- the guard are one statement and one CAS: a retraction that committed first has already moved [Action] off
    -- Approved, which is a clean zero-row outcome rather than a lost update. Zero rows means a precondition no longer
    -- held and the caller decides how to surface that.
    --
    -- Ordered before the singleton guard on purpose. The retraction paths lock [AccessRequest] and then read
    -- [AccessLease]; the guard below locks a range of [AccessLease]. Taking the guard first would invert the two
    -- operations' lock order and make them deadlock (error 1205), which neither caller retries.
    UPDATE [dbo].[AccessRequest]
    SET [Action] = 1 -- Approved: unchanged, the write is what matters
    WHERE
        [Id] = @AccessRequestId
        AND [RequesterId] = @RequesterId
        AND [Action] = 1 -- Approved
        AND [ExtensionOfLeaseId] IS NULL -- an extension applied in place on approval and never mints a lease
        AND [NotBefore] <= @Now
        AND [NotAfter] > @Now
        AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] AL WHERE AL.[AccessRequestId] = @AccessRequestId)

    IF @@ROWCOUNT = 0
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0
        RETURN
    END

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
    -- approved window. The preconditions are restated here as defence in depth -- the claim above already holds the
    -- row, so they cannot have changed -- and zero rows inserted still means a precondition no longer held.
    -- [IX_AccessLease_AccessRequestId] (unique) remains the backstop.
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

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_Cancel]
    @AccessRequestId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- An explicit transaction is required: the claim below only holds its row lock until the transaction ends, and in
    -- autocommit that is the end of the SELECT itself. XACT_ABORT keeps the pooled connection clean if either
    -- statement fails.
    SET XACT_ABORT ON

    BEGIN TRANSACTION AccessRequest_Cancel

    -- Claim the request row before probing for a produced lease. The UPDATE's NOT EXISTS is correlated to
    -- @AccessRequestId rather than to the row being updated, so the optimizer may evaluate it as a start-up filter
    -- *before* [AccessRequest] is ever locked -- and in that gap a concurrent [AccessLease_CreateFromApprovedRequest]
    -- can mint and commit, leaving this UPDATE to stamp Cancelled over a request that now holds a live lease. Access
    -- is governed by the lease alone once it exists, so that combination hands the requester the credential they
    -- withdrew from.
    --
    -- Taking the row under UPDLOCK first removes the ordering from the optimizer's hands. Activation claims the same
    -- row (see [AccessLease_CreateFromApprovedRequest]), so by the time the UPDATE runs there are only two states:
    -- activation has not claimed yet, and now blocks behind this transaction until it can, then fails its own CAS on
    -- [Action]; or it committed first, and the probe below sees the lease it minted. The value read is unused -- the
    -- guarded UPDATE stays the single arbiter of the transition -- because reading the row is simply how T-SQL takes
    -- a lock on it.
    DECLARE @Claimed TINYINT
    SELECT @Claimed = [Action]
    FROM [dbo].[AccessRequest] WITH (UPDLOCK, ROWLOCK)
    WHERE [Id] = @AccessRequestId

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

    COMMIT TRANSACTION AccessRequest_Cancel
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

    -- Claim the request row before probing for a produced lease, exactly as [AccessRequest_Cancel] does and for the
    -- same reason: the UPDATE's NOT EXISTS is correlated to @AccessRequestId rather than to the row being updated, so
    -- without this the optimizer may evaluate it before [AccessRequest] is locked and a concurrent
    -- [AccessLease_CreateFromApprovedRequest] can mint in the gap, leaving a Denied request holding a live lease.
    -- Activation claims the same row, so the two serialize on it. The value read is unused -- the guarded UPDATE
    -- stays the single arbiter of the transition -- because reading the row is simply how T-SQL takes a lock on it.
    DECLARE @Claimed TINYINT
    SELECT @Claimed = [Action]
    FROM [dbo].[AccessRequest] WITH (UPDLOCK, ROWLOCK)
    WHERE [Id] = @AccessRequestId

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
