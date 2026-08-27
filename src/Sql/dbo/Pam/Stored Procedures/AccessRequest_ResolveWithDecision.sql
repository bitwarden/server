CREATE PROCEDURE [dbo].[AccessRequest_ResolveWithDecision]
    @AccessRequestId UNIQUEIDENTIFIER,
    @Action TINYINT,
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

    -- Atomically record the human approver's action on an open request, together with their decision. The caller has
    -- already verified (and the application enforces) that no action is recorded yet; the WHERE guard keeps the write
    -- idempotent under a race so a second approver can't move an already-resolved request -- the column CAS decides
    -- who gets to write history, and the losing approver's verdict is never appended (@@ROWCOUNT > 0), which would
    -- leave the decision log contradicting the recorded action.
    --
    -- Approval does not mint the lease: the requester activates the approved request later via
    -- [AccessLease_CreateFromApprovedRequest]. The automatic path ([AccessRequest_CreateAutoApproved]) records the
    -- approved request the same way and likewise leaves the lease to be minted at activation.
    BEGIN TRANSACTION AccessRequest_Resolve

    UPDATE [dbo].[AccessRequest]
    SET [Action] = @Action,
        [ActionDate] = @Now
    WHERE [Id] = @AccessRequestId AND [Action] = 0 -- None (open)

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
