CREATE PROCEDURE [dbo].[AccessRequest_CancelWithDecision]
    @AccessRequestId UNIQUEIDENTIFIER,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @ApproverId UNIQUEIDENTIFIER,
    @Verdict TINYINT,
    @Comment NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- A managing approver retracts a not-yet-activated request (Pending, or an Approved request the requester has not
    -- activated): transition it to Denied and record the approver's human decision, mirroring
    -- [AccessRequest_ResolveWithDecision] but over the broader cancellable set. The WHERE guard is race-safe and
    -- refuses a request that has produced a lease (governed by the lease — revoke instead). The decision is inserted
    -- only when the transition actually happened (@@ROWCOUNT > 0), so a no-op never orphans an AccessDecision.
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
