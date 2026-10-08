CREATE PROCEDURE [dbo].[PamRotationConfig_ReadManyDue]
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The sweep's due phase; feeds OfferRotationCommand, one call per row.
    -- Matches [IX_PamRotationConfig_NextRotationAt] for a range seek, not a scan.
    SELECT C.*
    FROM [dbo].[PamRotationConfig] C
    INNER JOIN [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
    WHERE C.[Enabled] = 1
        AND C.[NextRotationAt] IS NOT NULL
        AND C.[NextRotationAt] <= @Now
        AND T.[Method] = 0 -- Automatic
        AND T.[Status] = 0 -- Active
        -- Until the timeout sweep records a timed-out job it still blocks, so its reschedule is never raced.
        AND NOT EXISTS (
            SELECT 1
            FROM [dbo].[PamRotationJob] J
            WHERE J.[RotationConfigId] = C.[Id]
                AND J.[Action] IN (0, 1) -- None, Claimed
                AND NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[PamRotationJobTimeoutSweep] S
                    WHERE S.[RotationJobId] = J.[Id]
                )
        )
END
