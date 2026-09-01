CREATE PROCEDURE [dbo].[PamRotationConfig_AnyByTargetSystem]
    @TargetSystemId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- DeleteTargetSystemCommand's guard: a target may only be removed once nothing is configured against it. The
    -- delete itself re-checks under lock (PamTargetSystem_DeleteWithAssignments); this read is what turns the common
    -- case into a refusal the admin can act on before anything is audited.
    SELECT 1
    FROM [dbo].[PamRotationConfig]
    WHERE [TargetSystemId] = @TargetSystemId
END
