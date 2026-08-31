CREATE PROCEDURE [dbo].[PamRotationJob_TimeoutDue]
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- JobTimesOut ("success wins"): a job with a Rotated attempt is excluded even if it is otherwise past ExpiresAt --
    -- a slow-but-successful report still wins the race against the timeout sweep. OUTPUT can't reach through the
    -- joins needed for the audit projection (config/org/cipher), so affected ids are captured in @Affected first and
    -- joined afterward. The job update and its attempt's Abandoned transition commit together so a crash between the
    -- two can never leave a stale Executing attempt behind a job that already moved on. XACT_ABORT guarantees
    -- rollback (and a clean pooled connection) on any error.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    DECLARE @Affected TABLE (
        [JobId] UNIQUEIDENTIFIER NOT NULL,
        [PreviousClaimedByDaemonId] UNIQUEIDENTIFIER NULL
    )

    UPDATE J
    SET J.[Status] = 4, -- TimedOut
        J.[ClaimedByDaemonId] = NULL,
        J.[ClaimedAt] = NULL
    OUTPUT deleted.[Id], deleted.[ClaimedByDaemonId] INTO @Affected ([JobId], [PreviousClaimedByDaemonId])
    FROM [dbo].[PamRotationJob] J
    WHERE J.[Status] IN (0, 1) -- Pending, Claimed
        AND J.[ExpiresAt] <= @Now
        AND NOT EXISTS (
            SELECT 1
            FROM [dbo].[PamRotationAttempt] AT
            WHERE AT.[JobId] = J.[Id] AND AT.[Status] = 1 -- Rotated
        )

    -- Abandon the executing attempt (if any) on each timed-out job; a Pending job that never got claimed has none.
    -- Abandoned attempts are never charged against the retry budget.
    UPDATE [dbo].[PamRotationAttempt]
    SET [Status] = 3, -- Abandoned
        [ResolvedDate] = @Now
    WHERE [JobId] IN (SELECT [JobId] FROM @Affected)
        AND [Status] = 0 -- Executing

    -- One row per timed-out job for audit emission; AttemptCount distinguishes unroutable (never claimed, zero
    -- attempts) from stuck (claimed at least once).
    SELECT
        AF.[JobId],
        C.[Id] AS [RotationConfigId],
        C.[OrganizationId],
        C.[CipherId],
        J.[Source],
        AF.[PreviousClaimedByDaemonId] AS [ClaimedByDaemonId],
        (SELECT COUNT(*) FROM [dbo].[PamRotationAttempt] AT WHERE AT.[JobId] = AF.[JobId]) AS [AttemptCount]
    FROM @Affected AF
    INNER JOIN [dbo].[PamRotationJob] J ON J.[Id] = AF.[JobId]
    INNER JOIN [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]

    COMMIT TRANSACTION
END
