CREATE PROCEDURE [dbo].[AccessRequest_Cancel]
    @AccessRequestId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The requester withdraws their own not-yet-activated request (Pending, or an Approved request they have not
    -- activated). Unlike [AccessRequest_CancelWithDecision], no AccessDecision is written: a cancellation is the
    -- requester acting on their own request, not an approver verdict. The WHERE guard keeps the write idempotent under
    -- a race and refuses a request that has already produced a lease (that access is governed by the lease, which must
    -- be revoked instead).
    UPDATE [dbo].[AccessRequest]
    SET [Status] = 3, -- Cancelled
        [ResolvedDate] = @Now
    WHERE [Id] = @AccessRequestId
        AND [Status] IN (0, 1) -- Pending or Approved
        AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] L WHERE L.[AccessRequestId] = @AccessRequestId)
END
