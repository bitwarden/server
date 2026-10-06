CREATE PROCEDURE [dbo].[PamRotationJob_ReleaseExpiredLeases]
    @Now DATETIME2(7),
    @OfflineAfterSeconds INT,
    @ReleaseDelaySeconds INT
AS
BEGIN
    SET NOCOUNT ON
    -- Releases require an expired lease and a stale heartbeat, never Action alone. A Rotated attempt always comes
    -- with a Succeeded job, so success wins without checking attempts.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    DECLARE @Affected TABLE (
        [JobId] UNIQUEIDENTIFIER NOT NULL,
        [PreviousClaimedByAccessConnectorId] UNIQUEIDENTIFIER NULL
    )

    UPDATE J
    SET J.[Action] = 0, -- None
        -- Uses the pre-clear ClaimedAt, still visible here, so re-claim time is exactly ExecuteBy.
        J.[NextClaimableAt] = DATEADD(SECOND, @ReleaseDelaySeconds, J.[ClaimedAt]),
        J.[ClaimedByAccessConnectorId] = NULL,
        J.[ClaimedAt] = NULL
    OUTPUT deleted.[Id], deleted.[ClaimedByAccessConnectorId] INTO @Affected ([JobId], [PreviousClaimedByAccessConnectorId])
    FROM [dbo].[PamRotationJob] J
    INNER JOIN [dbo].[PamAccessConnector] D ON D.[Id] = J.[ClaimedByAccessConnectorId]
    WHERE J.[Action] = 1 -- Claimed
        AND J.[ExpiresAt] > @Now -- a timed-out claim is the timeout sweep's, not a release
        AND DATEADD(SECOND, @ReleaseDelaySeconds, J.[ClaimedAt]) <= @Now
        AND (D.[LastHeartbeatAt] IS NULL OR D.[LastHeartbeatAt] < DATEADD(SECOND, -@OfflineAfterSeconds, @Now))

    -- Records when the released claim's attempt ended; it derives as Abandoned, which the retry budget never charges.
    UPDATE [dbo].[PamRotationAttempt]
    SET [ResolvedDate] = @Now
    WHERE [JobId] IN (SELECT [JobId] FROM @Affected)
        AND [Action] = 0 -- None
        AND [ResolvedDate] IS NULL

    -- One row per released job; ClaimedByAccessConnectorId is the pre-clear claimant, always non-null.
    SELECT
        AF.[JobId],
        C.[Id] AS [RotationConfigId],
        C.[OrganizationId],
        C.[CipherId],
        J.[Source],
        AF.[PreviousClaimedByAccessConnectorId] AS [ClaimedByAccessConnectorId]
    FROM @Affected AF
    INNER JOIN [dbo].[PamRotationJob] J ON J.[Id] = AF.[JobId]
    INNER JOIN [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]

    COMMIT TRANSACTION
END
