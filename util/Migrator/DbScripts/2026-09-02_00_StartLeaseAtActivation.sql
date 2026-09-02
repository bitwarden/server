-- Start a lease at activation, never backdated to the approved window's start (PM-42596).
--
-- [AccessLease_CreateFromApprovedRequest] minted the lease with [NotBefore] copied from AR.[NotBefore] -- the start
-- of the window the approver approved -- so a lease claimed to have begun at a moment access had not yet been
-- granted. On the on-demand path the window opens at submit, so the gap was the entire approval latency; on the
-- scheduled path it was the whole span between the window opening and the requester actually starting access.
--
-- A lease records when access began. The request already records what was asked for and granted, and the
-- [NotBefore] <= @Now precondition makes the window start a bound on when activation is *allowed*, not the lease's
-- start. Both readers of the column -- the requester's "My access" row and the audit trail's LeaseNotBefore -- were
-- overstating the lease.
--
-- [NotAfter] is deliberately unchanged: activating late shortens the lease rather than sliding its end out, because
-- the end is the promise the approver made about when access stops.
--
-- Authorization is unaffected in either direction. Activation already requires AR.[NotBefore] <= @Now, so the
-- minted start is in the past the instant it is written, and every live-lease predicate ([NotBefore] <= now AND
-- [NotAfter] > now) is satisfied exactly as before. Existing rows are left alone -- backfilling a truer start is
-- not possible, since the activation moment was never recorded separately (only [CreationDate], which is the
-- activation audit timestamp and is already correct).
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

    -- Activation of an approved request: mints the lease that authorizes access, running from @Now (this activation)
    -- to the request's approved end. [NotBefore] is deliberately @Now and NOT AR.[NotBefore] -- a lease records when
    -- access actually began, and is never backdated to the window start; the [NotBefore] <= @Now precondition below
    -- makes the request's window start a bound on when activation is allowed, not the lease's start. The end stays
    -- AR.[NotAfter]: activating late shortens the lease rather than sliding its end out.
    --
    -- @Now rather than the caller's lease copy, matching how every other column here is taken from the request
    -- rather than trusted from the caller. The preconditions are restated here as defence in depth -- the claim
    -- above already holds the row, so they cannot have changed -- and zero rows inserted still means a precondition
    -- no longer held. [IX_AccessLease_AccessRequestId] (unique) remains the backstop.
    INSERT INTO [dbo].[AccessLease]
    (
        [Id], [AccessRequestId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [Action], [NotBefore], [NotAfter], [RevokedDate], [RevokedBy], [CreationDate]
    )
    SELECT
        @AccessLeaseId, AR.[Id], AR.[OrganizationId], AR.[CollectionId], AR.[CipherId], AR.[RequesterId],
        0 /* None (no early end) */, @Now, AR.[NotAfter], NULL, NULL, @Now
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
