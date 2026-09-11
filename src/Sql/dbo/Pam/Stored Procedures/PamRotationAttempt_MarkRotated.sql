CREATE PROCEDURE [dbo].[PamRotationAttempt_MarkRotated]
    @AttemptId UNIQUEIDENTIFIER,
    @DaemonId UNIQUEIDENTIFIER,
    @SessionTermination TINYINT,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- CipherUpdated = 1 backstops VerifiedBeforeSuccess; guard failure takes RejectStaleSuccess.
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

    -- Clears claim fields leaving Claimed; the attempt already recorded who worked it.
    UPDATE [dbo].[PamRotationJob]
    SET [Status] = 2, -- Succeeded
        [ClaimedByDaemonId] = NULL,
        [ClaimedAt] = NULL
    WHERE [Id] = @JobId

    COMMIT TRANSACTION

    SELECT 1 -- Resolved
END
