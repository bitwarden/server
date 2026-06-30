-- PAM Credential Leasing: the requester can withdraw their own still-pending access request. The client already
-- issues DELETE /leasing/requests/{id} for this, but the server had no handler — this adds the missing transition.
--
-- [AccessRequest_Cancel] flips a Pending request to Cancelled (status 3) and stamps its ResolvedDate. No AccessDecision
-- row is written: a cancellation is the requester acting on their own request, not an approver verdict. The WHERE guard
-- keeps the write idempotent under a race so an already-resolved request is left untouched.
--
-- Feature is behind the pm-37044-pam-v-0 flag (unshipped POC); server + migration deploy together.

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_Cancel]
    @AccessRequestId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE [dbo].[AccessRequest]
    SET [Status] = 3, -- Cancelled
        [ResolvedDate] = @Now
    WHERE [Id] = @AccessRequestId AND [Status] = 0 -- Pending
END
GO
