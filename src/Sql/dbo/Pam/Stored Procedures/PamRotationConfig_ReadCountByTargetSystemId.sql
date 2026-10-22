CREATE PROCEDURE [dbo].[PamRotationConfig_ReadCountByTargetSystemId]
    @TargetSystemId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- DeleteTargetSystemCommand's guard; the delete itself re-checks under lock separately. Returns 0 or 1, since
    -- EXISTS stops at the first config.
    SELECT
        CASE
            WHEN EXISTS (
                SELECT
                    1
                FROM
                    [dbo].[PamRotationConfigView]
                WHERE
                    [TargetSystemId] = @TargetSystemId
            ) THEN 1
            ELSE 0
        END
END
