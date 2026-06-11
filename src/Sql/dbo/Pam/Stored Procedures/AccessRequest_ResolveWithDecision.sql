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

    -- Atomically resolve a pending request and record the human approver's decision. The caller has already verified
    -- (and the application enforces) that the request is still Pending; the WHERE guard keeps the write idempotent
    -- under a race so a second approver can't move an already-resolved request.
    --
    -- Approval does not mint the lease: the requester activates the approved request later via
    -- [AccessLease_CreateFromApprovedRequest]. The automatic path ([AccessRequest_CreateAutoApproved]) records the
    -- approved request the same way and likewise leaves the lease to be minted at activation.
    BEGIN TRANSACTION AccessRequest_Resolve

    UPDATE [dbo].[AccessRequest]
    SET [Status] = @Status,
        [ResolvedDate] = @Now
    WHERE [Id] = @AccessRequestId AND [Status] = 0 -- Pending

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

    COMMIT TRANSACTION AccessRequest_Resolve
END
