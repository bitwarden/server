CREATE PROCEDURE [dbo].[AccessLease_UpdateRevoked]
    @AccessLeaseId UNIQUEIDENTIFIER,
    @Action TINYINT,
    @RevokedBy UNIQUEIDENTIFIER,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @Reason NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- XACT_ABORT rolls back errors that skip CATCH (batch-aborting errors, client timeouts).
    SET XACT_ABORT ON

    -- Ends a running lease (2 Revoked or 3 Cancelled), idempotently; feeds a Deny AccessDecision.
    DECLARE @Ended TABLE ([AccessRequestId] UNIQUEIDENTIFIER)

    BEGIN TRY
        BEGIN TRANSACTION

        UPDATE [dbo].[AccessLease]
        SET [Action] = @Action,
            [RevokedDate] = @Now,
            [RevokedBy] = @RevokedBy
        OUTPUT INSERTED.[AccessRequestId] INTO @Ended
        WHERE [Id] = @AccessLeaseId
            AND [Action] = 0 -- None (no early end)
            AND [NotAfter] > @Now

        INSERT INTO [dbo].[AccessDecision]
        (
            [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
            [Verdict], [Comment], [EvaluationContext], [CreationDate]
        )
        SELECT
            @AccessDecisionId, E.[AccessRequestId], 1 /* Human */, @RevokedBy, NULL,
            0 /* Deny */, @Reason, NULL, @Now
        FROM @Ended E

        COMMIT TRANSACTION
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
