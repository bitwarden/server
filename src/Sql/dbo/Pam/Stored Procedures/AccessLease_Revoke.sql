CREATE PROCEDURE [dbo].[AccessLease_Revoke]
    @AccessLeaseId UNIQUEIDENTIFIER,
    @AccessRequestId UNIQUEIDENTIFIER,
    @Status TINYINT,
    @RevokedBy UNIQUEIDENTIFIER,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @Reason NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Atomically end an active lease and capture who/why. @Status is the end state: 2 (Revoked) when an operator ended
    -- it, 3 (Cancelled) when the holder ended their own; RevokedDate/RevokedBy record when/who either way. The reason
    -- has no dedicated column, so it is preserved as a human AccessDecision (Deny) against the lease's originating
    -- request, keeping the audit trail without a schema change. The WHERE guard keeps the end idempotent if two
    -- callers race.
    BEGIN TRANSACTION AccessLease_Revoke

    UPDATE [dbo].[AccessLease]
    SET [Status] = @Status,
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
        0 /* Deny */, @Reason, NULL, @Now
    )

    COMMIT TRANSACTION AccessLease_Revoke
END
