CREATE PROCEDURE [dbo].[PamRotationConfig_ReadCountByTargetSystemId]
    @TargetSystemId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- DeleteTargetSystemCommand's guard; the delete itself re-checks under lock separately.
    SELECT
        COUNT(1)
    FROM
        [dbo].[PamRotationConfigView]
    WHERE
        [TargetSystemId] = @TargetSystemId
END
