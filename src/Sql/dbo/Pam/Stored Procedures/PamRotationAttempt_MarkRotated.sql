CREATE PROCEDURE [dbo].[PamRotationAttempt_MarkRotated]
    @AttemptId UNIQUEIDENTIFIER,
    @DaemonId UNIQUEIDENTIFIER,
    @SessionTermination TINYINT,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- RecordRotationSucceeded -> MarkJobSucceeded. CipherUpdated = 1 is the VerifiedBeforeSuccess backstop: a success
    -- report cannot resolve an attempt whose cipher write was never accepted. Guard failure (unknown/stale attempt,
    -- wrong claimant, no cipher write, or the job already moved on) takes the RejectStaleSuccess path -- the caller
    -- audits report_rejected, nothing changes. XACT_ABORT guarantees rollback (and a clean pooled connection) on any
    -- error.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    DECLARE @JobId UNIQUEIDENTIFIER

    SELECT @JobId = J.[Id]
    FROM [dbo].[PamRotationAttempt] AT
    INNER JOIN [dbo].[PamRotationJob] J WITH (UPDLOCK) ON J.[Id] = AT.[JobId]
    WHERE AT.[Id] = @AttemptId
        AND AT.[Status] = 0 -- Executing
        AND AT.[ClaimedByDaemonId] = @DaemonId
        AND AT.[CipherUpdated] = 1
        AND J.[Status] = 1 -- Claimed

    IF @JobId IS NULL
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0 -- Rejected
        RETURN
    END

    UPDATE [dbo].[PamRotationAttempt]
    SET [Status] = 1, -- Rotated
        [SessionTermination] = @SessionTermination,
        [ResolvedDate] = @Now
    WHERE [Id] = @AttemptId

    -- Every transition out of Claimed nulls the claim fields; the executing daemon's identity for this try is
    -- already permanently recorded on the attempt above.
    UPDATE [dbo].[PamRotationJob]
    SET [Status] = 2, -- Succeeded
        [ClaimedByDaemonId] = NULL,
        [ClaimedAt] = NULL
    WHERE [Id] = @JobId

    COMMIT TRANSACTION

    SELECT 1 -- Resolved
END
