CREATE PROCEDURE [dbo].[PamRotationConfig_AnyByTargetSystemWithTerminateSessions]
    @TargetSystemId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- UpdateTargetSystemPolicyCommand's guard: can't disable SupportsSessionTermination while a config opts in.
    SELECT 1
    FROM [dbo].[PamRotationConfig]
    WHERE [TargetSystemId] = @TargetSystemId AND [TerminateSessions] = 1
END
