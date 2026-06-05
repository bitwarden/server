CREATE PROCEDURE [dbo].[Lease_Revoke]
    @LeaseId UNIQUEIDENTIFIER,
    @LeaseRequestId UNIQUEIDENTIFIER,
    @RevokedBy UNIQUEIDENTIFIER,
    @LeaseDecisionId UNIQUEIDENTIFIER,
    @Reason NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Atomically revoke an active lease and capture who/why. The revocation reason has no dedicated column, so it is
    -- preserved as a human LeaseDecision (Deny) against the lease's originating request, keeping the audit trail
    -- without a schema change. The WHERE guard keeps revocation idempotent if two approvers race.
    BEGIN TRANSACTION Lease_Revoke

    UPDATE [dbo].[Lease]
    SET [Status] = 2 /* Revoked */,
        [RevokedDate] = @Now,
        [RevokedBy] = @RevokedBy
    WHERE [Id] = @LeaseId AND [Status] = 0 -- Active

    INSERT INTO [dbo].[LeaseDecision]
    (
        [Id], [LeaseRequestId], [DeciderKind], [ApproverId], [PolicyKind],
        [Decision], [Comment], [EvaluationContext], [CreationDate]
    )
    VALUES
    (
        @LeaseDecisionId, @LeaseRequestId, 1 /* Human */, @RevokedBy, NULL,
        1 /* Deny */, @Reason, NULL, @Now
    )

    COMMIT TRANSACTION Lease_Revoke
END
