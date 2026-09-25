CREATE PROCEDURE [dbo].[AccessRequest_UpdateCancelled]
    @AccessRequestId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- XACT_ABORT rolls back errors that skip CATCH (batch-aborting errors, client timeouts).
    SET XACT_ABORT ON

    BEGIN TRY
        -- Explicit transaction needed; autocommit would release the claim's lock before the check.
        BEGIN TRANSACTION

        -- Claims the row before the lease probe so a concurrent activation can't mint meanwhile.
        DECLARE @Claimed TINYINT
        SELECT @Claimed = [Action]
        FROM [dbo].[AccessRequest] WITH (UPDLOCK, ROWLOCK)
        WHERE [Id] = @AccessRequestId

        -- Requester withdrawal of a not-yet-activated request; no AccessDecision, since it isn't an approver verdict.
        UPDATE [dbo].[AccessRequest]
        SET [Action] = 3, -- Cancelled
            [ActionDate] = @Now
        WHERE [Id] = @AccessRequestId
            AND [Action] IN (0, 1) -- None (open) or Approved
            AND [NotAfter] > @Now
            AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] L WHERE L.[AccessRequestId] = @AccessRequestId)

        DECLARE @Rows INT = @@ROWCOUNT

        COMMIT TRANSACTION

        -- 1 when this call withdrew the request, 0 when it was no longer withdrawable.
        SELECT CAST(CASE WHEN @Rows > 0 THEN 1 ELSE 0 END AS BIT)
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
