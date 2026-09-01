CREATE PROCEDURE [dbo].[AccessLease_CreateFromApprovedRequest]
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
