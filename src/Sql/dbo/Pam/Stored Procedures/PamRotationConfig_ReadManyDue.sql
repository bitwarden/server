CREATE PROCEDURE [dbo].[PamRotationConfig_ReadManyDue]
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The sweep's due phase (spec RotationDue): enabled, automatic, active-target configs whose schedule has come
    -- due, with no job already in flight (OfferRotation is the single creation point -- this feeds it, one
    -- OfferRotationCommand call per row). Enabled + NextRotationAt IS NOT NULL matches
    -- [IX_PamRotationConfig_NextRotationAt] so the scan is a narrow range seek, not a table scan.
    SELECT C.*
    FROM [dbo].[PamRotationConfig] C
    INNER JOIN [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
    WHERE C.[Enabled] = 1
        AND C.[NextRotationAt] IS NOT NULL
        AND C.[NextRotationAt] <= @Now
        AND T.[Method] = 0 -- Automatic
        AND T.[Status] = 0 -- Active
        AND NOT EXISTS (
            SELECT 1
            FROM [dbo].[PamRotationJob] J
            WHERE J.[RotationConfigId] = C.[Id] AND J.[Status] IN (0, 1) -- Pending, Claimed
        )
END
