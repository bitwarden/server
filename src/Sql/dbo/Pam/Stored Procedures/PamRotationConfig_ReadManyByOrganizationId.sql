CREATE PROCEDURE [dbo].[PamRotationConfig_ReadManyByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Schedule-list view: denormalizes target name/method, computes HasActiveJob to avoid an N+1/round trip.
    -- A timed-out job stays active until the timeout sweep records it.
    SELECT
        C.*,
        T.[Name] AS [TargetSystemName],
        T.[Method] AS [TargetSystemMethod],
        CASE WHEN EXISTS (
            SELECT 1
            FROM [dbo].[PamRotationJob] J
            WHERE J.[RotationConfigId] = C.[Id]
                AND J.[Action] IN (0, 1) -- None, Claimed
                AND NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[PamRotationJobTimeoutSweep] S
                    WHERE S.[RotationJobId] = J.[Id]
                )
        ) THEN 1 ELSE 0 END AS [HasActiveJob]
    FROM [dbo].[PamRotationConfig] C
    INNER JOIN [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
    WHERE C.[OrganizationId] = @OrganizationId
END
