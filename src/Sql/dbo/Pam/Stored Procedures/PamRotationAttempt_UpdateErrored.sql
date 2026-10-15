CREATE PROCEDURE [dbo].[PamRotationAttempt_UpdateErrored]
    @AttemptId UNIQUEIDENTIFIER,
    @AccessConnectorId UNIQUEIDENTIFIER,
    @FailureReason NVARCHAR(500) = NULL,
    @SyncState TINYINT,
    @Now DATETIME2(7),
    @MaxAttempts INT,
    @RetryBaseDelaySeconds INT
AS
BEGIN
    SET NOCOUNT ON
    -- @FailureReason is pre-bounded by the caller's zero-knowledge contract; guard failure takes
    -- RejectStaleFailureReport.
    SET XACT_ABORT ON

    DECLARE @JobId UNIQUEIDENTIFIER
    DECLARE @ErroredCount INT
    -- The status the job derives as right after this write; the guard saw it unexpired.
    DECLARE @JobStatus TINYINT

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
            AND J.[Action] = 1 -- Claimed
            AND J.[ExpirationDate] > @Now
            AND J.[ClaimedByAccessConnectorId] = @AccessConnectorId
            AND J.[ClaimedDate] = AT.[CreationDate]

        IF @JobId IS NULL
        BEGIN
            ROLLBACK TRANSACTION
            SELECT
                0 AS [Outcome], -- Rejected
                NULL AS [JobStatus],
                NULL AS [ErroredAttemptCount]
            RETURN
        END

        UPDATE
            [dbo].[PamRotationAttempt]
        SET
            [Action] = 2, -- Errored
            [FailureReason] = @FailureReason,
            [SyncState] = @SyncState,
            [ResolvedDate] = @Now
        WHERE
            [Id] = @AttemptId

        -- Only Errored attempts count toward the retry budget; Abandoned (released/timed-out) tries never do.
        SELECT
            @ErroredCount = COUNT(*)
        FROM
            [dbo].[PamRotationAttempt]
        WHERE
            [JobId] = @JobId
            AND [Action] = 2 -- Errored

        IF @ErroredCount < @MaxAttempts
        BEGIN
            SET @JobStatus = 0 -- Pending

            UPDATE
                [dbo].[PamRotationJob]
            SET
                [Action] = 0, -- None
                [ClaimedByAccessConnectorId] = NULL,
                [ClaimedDate] = NULL,
                [NextClaimableDate] = DATEADD(
                    SECOND,
                    CAST(@RetryBaseDelaySeconds * POWER(2, @ErroredCount - 1) AS INT),
                    @Now
                )
            WHERE
                [Id] = @JobId
        END
        ELSE
        BEGIN
            SET @JobStatus = 3 -- Failed

            UPDATE
                [dbo].[PamRotationJob]
            SET
                [Action] = 3, -- Failed
                [ClaimedByAccessConnectorId] = NULL,
                [ClaimedDate] = NULL
            WHERE
                [Id] = @JobId
        END

        COMMIT TRANSACTION

        SELECT
            1 AS [Outcome], -- Resolved
            @JobStatus AS [JobStatus],
            @ErroredCount AS [ErroredAttemptCount]
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
