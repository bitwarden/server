CREATE PROCEDURE [dbo].[PamRotationJob_ReadManyClaimableByDaemonId]
    @DaemonId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The daemon poll: jobs this daemon may claim right now. Re-derives every eligibility condition
    -- PamRotationJob_Claim itself re-checks (config enabled, target active, an assignment exists, and -- defense in
    -- depth -- the daemon's own org matches the config's org) so the list a daemon sees and what it can actually
    -- claim never diverge.
    SELECT J.*
    FROM [dbo].[PamRotationJob] J
    INNER JOIN [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]
    INNER JOIN [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
    INNER JOIN [dbo].[PamDaemonTargetAssignment] A ON A.[DaemonId] = @DaemonId AND A.[TargetSystemId] = C.[TargetSystemId]
    INNER JOIN [dbo].[PamDaemon] D ON D.[Id] = @DaemonId AND D.[OrganizationId] = C.[OrganizationId]
    WHERE J.[Status] = 0 -- Pending
        AND J.[NextClaimableAt] <= @Now
        AND C.[Enabled] = 1
        AND T.[Status] = 0 -- Active
END
