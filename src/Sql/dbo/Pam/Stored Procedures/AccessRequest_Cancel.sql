CREATE PROCEDURE [dbo].[AccessRequest_Cancel]
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
