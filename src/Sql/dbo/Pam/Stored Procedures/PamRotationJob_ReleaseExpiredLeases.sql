CREATE PROCEDURE [dbo].[PamRotationJob_ReleaseExpiredLeases]
    @Now DATETIME2(7),
    @OfflineAfterSeconds INT,
    @ReleaseDelaySeconds INT
AS
BEGIN
    SET NOCOUNT ON
    -- DaemonConnectionDropsReleaseJobs -> ReleaseJob -> AbandonAttempt, with the lease-respecting timing from plan §5:
    -- release only fires once BOTH the claim's lease has expired (now >= ExecuteBy, i.e. ClaimedAt + ReleaseDelay) AND
    -- the claimant's heartbeat is stale -- never on daemon Status alone, so a revoked daemon's jobs release too once
    -- its heartbeats actually stop. A job with a Rotated attempt is excluded ("success wins", same as the timeout
    -- sweep): a slow-but-live daemon whose report lands inside its lease still wins. OUTPUT can't reach through the
    -- joins needed for the audit projection, so affected ids are captured in @Affected first and joined afterward.
    -- XACT_ABORT guarantees rollback (and a clean pooled connection) on any error.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    DECLARE @Affected TABLE (
        [JobId] UNIQUEIDENTIFIER NOT NULL,
        [PreviousClaimedByDaemonId] UNIQUEIDENTIFIER NULL
    )

    UPDATE J
    SET J.[Status] = 0, -- Pending
        -- Computed from the pre-clear ClaimedAt (this UPDATE's FROM/JOIN still sees the old value here), so the
        -- re-claim time is exactly ExecuteBy regardless of whether release fires at that instant or later.
        J.[NextClaimableAt] = DATEADD(SECOND, @ReleaseDelaySeconds, J.[ClaimedAt]),
        J.[ClaimedByDaemonId] = NULL,
        J.[ClaimedAt] = NULL
    OUTPUT deleted.[Id], deleted.[ClaimedByDaemonId] INTO @Affected ([JobId], [PreviousClaimedByDaemonId])
    FROM [dbo].[PamRotationJob] J
    INNER JOIN [dbo].[PamDaemon] D ON D.[Id] = J.[ClaimedByDaemonId]
    WHERE J.[Status] = 1 -- Claimed
        AND DATEADD(SECOND, @ReleaseDelaySeconds, J.[ClaimedAt]) <= @Now
        AND (D.[LastHeartbeatAt] IS NULL OR D.[LastHeartbeatAt] < DATEADD(SECOND, -@OfflineAfterSeconds, @Now))
        AND NOT EXISTS (
            SELECT 1
            FROM [dbo].[PamRotationAttempt] AT
            WHERE AT.[JobId] = J.[Id] AND AT.[Status] = 1 -- Rotated
        )

    -- Abandoned attempts are never charged against the retry budget.
    UPDATE [dbo].[PamRotationAttempt]
    SET [Status] = 3, -- Abandoned
        [ResolvedDate] = @Now
    WHERE [JobId] IN (SELECT [JobId] FROM @Affected)
        AND [Status] = 0 -- Executing

    -- One row per released job for audit emission. ClaimedByDaemonId here is the pre-clear claimant (always
    -- non-null: only Claimed jobs are released).
    SELECT
        AF.[JobId],
        C.[Id] AS [RotationConfigId],
        C.[OrganizationId],
        C.[CipherId],
        J.[Source],
        AF.[PreviousClaimedByDaemonId] AS [ClaimedByDaemonId]
    FROM @Affected AF
    INNER JOIN [dbo].[PamRotationJob] J ON J.[Id] = AF.[JobId]
    INNER JOIN [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]

    COMMIT TRANSACTION
END
