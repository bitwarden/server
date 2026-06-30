CREATE PROCEDURE [dbo].[AccessRequest_MarkActivationRejected]
    @AccessRequestId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- A refused activation (e.g. single-active-lease contention) leaves the approved request re-activatable; record the
    -- latest refusal for the audit trail (last-only). The WHERE guard keeps it a no-op unless the request is still
    -- Approved and has produced no lease, so it never stamps an activated or already-used request.
    UPDATE [dbo].[AccessRequest]
    SET [RejectedDate] = @Now
    WHERE [Id] = @AccessRequestId
        AND [Status] = 1 -- Approved
        AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] WHERE [AccessRequestId] = @AccessRequestId)
END
