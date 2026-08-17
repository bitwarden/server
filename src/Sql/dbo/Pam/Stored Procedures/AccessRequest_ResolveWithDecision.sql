CREATE PROCEDURE [dbo].[AccessRequest_ResolveWithDecision]
    @AccessRequestId UNIQUEIDENTIFIER,
    @Status TINYINT,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @ApproverId UNIQUEIDENTIFIER,
    @Verdict TINYINT,
    @Comment NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- XACT_ABORT rolls the transaction back as a unit if either write fails. Without it a constraint violation aborts
    -- only the offending statement, execution falls through to the COMMIT, and the other half is persisted alone.
    SET XACT_ABORT ON

    -- Atomically resolve a pending request and record the human approver's decision. The caller has already verified
    -- (and the application enforces) that the request is still Pending; the WHERE guard keeps the write idempotent
    -- under a race so a second approver can't move an already-resolved request. The decision is recorded only when the
    -- transition actually happened (@@ROWCOUNT > 0), so a losing approver's verdict is never appended to a request
    -- they did not resolve -- which would leave the decision log contradicting the request's status.
    --
    -- Approval does not mint the lease: the requester activates the approved request later via
    -- [AccessLease_CreateFromApprovedRequest]. The automatic path ([AccessRequest_CreateAutoApproved]) records the
    -- approved request the same way and likewise leaves the lease to be minted at activation.
    BEGIN TRANSACTION AccessRequest_Resolve

    UPDATE [dbo].[AccessRequest]
    SET [Status] = @Status,
        [ResolvedDate] = @Now
    WHERE [Id] = @AccessRequestId AND [Status] = 0 -- Pending

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

    COMMIT TRANSACTION AccessRequest_Resolve
END
