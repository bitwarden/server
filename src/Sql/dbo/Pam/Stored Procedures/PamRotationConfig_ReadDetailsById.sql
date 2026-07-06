CREATE PROCEDURE [dbo].[PamRotationConfig_ReadDetailsById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- The config detail page's header projection (IPamRotationConfigRepository.GetDetailsByIdAsync): the target's
    -- display name/method denormalized, plus a computed HasActiveJob so the caller can gate Delete/UpdateAccount
    -- without a second round trip. "Active" mirrors PamRotationJob_Create's guard: Pending or Claimed.
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
    WHERE C.[Id] = @Id
END
