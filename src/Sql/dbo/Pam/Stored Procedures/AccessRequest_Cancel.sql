CREATE PROCEDURE [dbo].[AccessRequest_Cancel]
    @AccessRequestId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The requester withdraws their own still-pending request. Unlike [AccessRequest_ResolveWithDecision], no
    -- AccessDecision is written: a cancellation is the requester acting on their own request, not an approver verdict.
    -- The caller (CancelAccessRequestCommand) has already verified ownership and that the request is Pending; the
    -- WHERE guard keeps the write idempotent under a race (double-click, or a concurrent auto/human resolution) so a
    -- request that has already left Pending is left untouched.
    UPDATE [dbo].[AccessRequest]
    SET [Status] = 3, -- Cancelled
        [ResolvedDate] = @Now
    WHERE [Id] = @AccessRequestId AND [Status] = 0 -- Pending
END
