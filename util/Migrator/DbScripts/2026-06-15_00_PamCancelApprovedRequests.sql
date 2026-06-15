-- PAM Credential Leasing: broaden cancellation so a not-yet-activated access request can be ended by either the
-- requester or a managing approver. A request is cancellable while Pending or Approved-without-a-lease; once it has
-- produced a lease it is governed by that lease (revoke instead).
--
-- [AccessRequest_Cancel] (requester path): flips a Pending/Approved request with no lease to Cancelled (status 3),
-- stamps ResolvedDate. No AccessDecision is written.
-- [AccessRequest_CancelWithDecision] (approver path): flips it to Denied (status 2) and records the approver's human
-- Deny decision, mirroring [AccessRequest_ResolveWithDecision]. The decision is inserted only when the transition
-- happens (@@ROWCOUNT > 0), so a no-op never orphans a decision.
-- Both procs guard on Status IN (0,1) AND no AccessLease for the request, keeping the write race-safe.
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
    WHERE [Id] = @AccessRequestId
        AND [Status] IN (0, 1) -- Pending or Approved
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

    BEGIN TRANSACTION AccessRequest_CancelWithDecision

    UPDATE [dbo].[AccessRequest]
    SET [Status] = 2, -- Denied
        [ResolvedDate] = @Now
    WHERE [Id] = @AccessRequestId
        AND [Status] IN (0, 1) -- Pending or Approved
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
