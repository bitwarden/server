CREATE PROCEDURE [dbo].[PamRotationConfig_ReadManyByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Schedule-list view: denormalizes target name/method, computes HasActiveJob to avoid an N+1/round trip.
    SELECT
        C.*,
        T.[Name] AS [TargetSystemName],
        T.[Method] AS [TargetSystemMethod],
        CASE WHEN EXISTS (
            SELECT 1
            FROM [dbo].[PamRotationJob] J
            WHERE J.[RotationConfigId] = C.[Id] AND J.[Status] IN (0, 1) -- Pending, Claimed
        ) THEN 1 ELSE 0 END AS [HasActiveJob]
    FROM [dbo].[PamRotationConfig] C
    INNER JOIN [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
    WHERE C.[OrganizationId] = @OrganizationId
END
