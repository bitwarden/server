CREATE PROCEDURE [dbo].[AccessRequest_Cancel]
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
