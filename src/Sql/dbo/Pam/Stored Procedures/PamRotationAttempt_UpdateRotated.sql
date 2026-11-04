CREATE PROCEDURE [dbo].[PamRotationAttempt_UpdateRotated]
    @AttemptId UNIQUEIDENTIFIER,
    @AccessConnectorId UNIQUEIDENTIFIER,
    @SessionTermination TINYINT,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- CipherUpdated = 1 backstops VerifiedBeforeSuccess; guard failure takes RejectStaleSuccess.
    SET XACT_ABORT ON

    DECLARE @JobId UNIQUEIDENTIFIER

    BEGIN TRY
        BEGIN TRANSACTION

        -- Executing is derived: unreported, on an unexpired claim, and created by the claim the job still records.
        SELECT
            @JobId = J.[Id]
        FROM
            [dbo].[PamRotationAttempt] AT
        INNER JOIN
            [dbo].[PamRotationJob] J WITH (UPDLOCK) ON J.[Id] = AT.[JobId]
        WHERE
            AT.[Id] = @AttemptId
            AND AT.[Action] = 0 -- None
            AND AT.[ClaimedByAccessConnectorId] = @AccessConnectorId
            AND AT.[CipherUpdated] = 1
            AND J.[Action] = 1 -- Claimed
            AND J.[ExpirationDate] > @Now
            AND J.[ClaimedByAccessConnectorId] = @AccessConnectorId
            AND J.[ClaimedDate] = AT.[CreationDate]

        IF @JobId IS NULL
        BEGIN
            ROLLBACK TRANSACTION
            SELECT 0 -- Rejected
            RETURN
        END

        UPDATE
            [dbo].[PamRotationAttempt]
        SET
            [Action] = 1, -- Rotated
            [SessionTermination] = @SessionTermination,
            [ResolvedDate] = @Now
        WHERE
            [Id] = @AttemptId

        -- Written with the attempt, so a Rotated attempt always has a Succeeded job: success wins by construction.
        UPDATE
            [dbo].[PamRotationJob]
        SET
            [Action] = 2, -- Succeeded
            [ClaimedByAccessConnectorId] = NULL,
            [ClaimedDate] = NULL
        WHERE
            [Id] = @JobId

        COMMIT TRANSACTION

        SELECT 1 -- Resolved
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
