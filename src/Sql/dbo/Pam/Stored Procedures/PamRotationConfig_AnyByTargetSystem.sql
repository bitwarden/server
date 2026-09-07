CREATE PROCEDURE [dbo].[PamRotationConfig_AnyByTargetSystem]
    @TargetSystemId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- DeleteTargetSystemCommand's guard; the delete itself re-checks under lock separately.
    SELECT 1
    FROM [dbo].[PamRotationConfig]
    WHERE [TargetSystemId] = @TargetSystemId
END
