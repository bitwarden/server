CREATE PROCEDURE [dbo].[LeaseRequest_ResolveWithDecision]
    @LeaseRequestId UNIQUEIDENTIFIER,
    @Status TINYINT,
    @LeaseDecisionId UNIQUEIDENTIFIER,
    @ApproverId UNIQUEIDENTIFIER,
    @Decision TINYINT,
    @Comment NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Atomically resolve a pending request and record the human approver's decision. The caller has already verified
    -- (and the application enforces) that the request is still Pending; the WHERE guard keeps the write idempotent
    -- under a race so a second approver can't move an already-resolved request.
    BEGIN TRANSACTION LeaseRequest_ResolveWithDecision

    UPDATE [dbo].[LeaseRequest]
    SET [Status] = @Status,
        [ResolvedDate] = @Now
    WHERE [Id] = @LeaseRequestId AND [Status] = 0 -- Pending

    INSERT INTO [dbo].[LeaseDecision]
    (
        [Id], [LeaseRequestId], [DeciderKind], [ApproverId], [PolicyKind],
        [Decision], [Comment], [EvaluationContext], [CreationDate]
    )
    VALUES
    (
        @LeaseDecisionId, @LeaseRequestId, 1 /* Human */, @ApproverId, NULL,
        @Decision, @Comment, NULL, @Now
    )

    COMMIT TRANSACTION LeaseRequest_ResolveWithDecision
END
