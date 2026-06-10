CREATE PROCEDURE [dbo].[AccessLease_Revoke]
    @AccessLeaseId UNIQUEIDENTIFIER,
    @AccessRequestId UNIQUEIDENTIFIER,
    @RevokedBy UNIQUEIDENTIFIER,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @Reason NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Atomically revoke an active lease and capture who/why. The revocation reason has no dedicated column, so it is
    -- preserved as a human AccessDecision (Deny) against the lease's originating request, keeping the audit trail
    -- without a schema change. The WHERE guard keeps revocation idempotent if two approvers race.
    BEGIN TRANSACTION AccessLease_Revoke

    UPDATE [dbo].[AccessLease]
    SET [Status] = 2 /* Revoked */,
        [RevokedDate] = @Now,
        [RevokedBy] = @RevokedBy
    WHERE [Id] = @AccessLeaseId AND [Status] = 0 -- Active

    INSERT INTO [dbo].[AccessDecision]
    (
        [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
        [Verdict], [Comment], [EvaluationContext], [CreationDate]
    )
    VALUES
    (
        @AccessDecisionId, @AccessRequestId, 1 /* Human */, @RevokedBy, NULL,
        1 /* Deny */, @Reason, NULL, @Now
    )

    COMMIT TRANSACTION AccessLease_Revoke
END
