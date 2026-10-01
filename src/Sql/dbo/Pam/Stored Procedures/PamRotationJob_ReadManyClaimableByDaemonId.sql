CREATE PROCEDURE [dbo].[PamRotationJob_ReadManyClaimableByDaemonId]
    @DaemonId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The daemon poll; re-derives PamRotationJob_Claim's eligibility so the poll list matches what's claimable.
    SELECT J.*, C.[TargetSystemId]
    FROM [dbo].[PamRotationJob] J
    INNER JOIN [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]
    INNER JOIN [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
    INNER JOIN [dbo].[PamDaemonTargetAssignment] A ON A.[DaemonId] = @DaemonId AND A.[TargetSystemId] = C.[TargetSystemId]
    INNER JOIN [dbo].[PamDaemon] D ON D.[Id] = @DaemonId AND D.[OrganizationId] = C.[OrganizationId] AND D.[Status] = 0 -- Enabled
    WHERE J.[Status] = 0 -- Pending
        AND J.[NextClaimableAt] <= @Now
        AND C.[Enabled] = 1
        AND T.[Status] = 0 -- Active
END
