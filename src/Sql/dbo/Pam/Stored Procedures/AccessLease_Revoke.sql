CREATE PROCEDURE [dbo].[AccessLease_Revoke]
    @AccessLeaseId UNIQUEIDENTIFIER,
    @Status TINYINT,
    @RevokedBy UNIQUEIDENTIFIER,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @Reason NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- XACT_ABORT rolls the transaction back as a unit if either write fails. Without it a constraint violation aborts
    -- only the offending statement, execution falls through to the COMMIT, and the other half is persisted alone.
    SET XACT_ABORT ON

    -- Atomically end an active lease and capture who/why. @Status is the end state: 2 (Revoked) when an operator ended
    -- it, 3 (Cancelled) when the holder ended their own; RevokedDate/RevokedBy record when/who either way. The reason
    -- has no dedicated column, so it is preserved as a human AccessDecision (Deny) against the lease's originating
    -- request, keeping the audit trail without a schema change. The WHERE guard keeps the end idempotent if two
    -- callers race.
    --
    -- OUTPUT captures the ended lease's own AccessRequestId, which does double duty: the decision is written only when
    -- the transition actually happened (a repeat revoke ends nothing and appends nothing), and it is written against
    -- the request that lease actually came from rather than one supplied by the caller.
    DECLARE @Ended TABLE ([AccessRequestId] UNIQUEIDENTIFIER)

    BEGIN TRANSACTION AccessLease_Revoke

    UPDATE [dbo].[AccessLease]
    SET [Status] = @Status,
        [RevokedDate] = @Now,
        [RevokedBy] = @RevokedBy
    OUTPUT INSERTED.[AccessRequestId] INTO @Ended
    WHERE [Id] = @AccessLeaseId AND [Status] = 0 -- Active

    INSERT INTO [dbo].[AccessDecision]
    (
        [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
        [Verdict], [Comment], [EvaluationContext], [CreationDate]
    )
    SELECT
        @AccessDecisionId, E.[AccessRequestId], 1 /* Human */, @RevokedBy, NULL,
        0 /* Deny */, @Reason, NULL, @Now
    FROM @Ended E

    COMMIT TRANSACTION AccessLease_Revoke
END
