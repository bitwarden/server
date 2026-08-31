CREATE PROCEDURE [dbo].[PamRotationConfig_AnyByTargetSystemWithTerminateSessions]
    @TargetSystemId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- UpdateTargetSystemPolicyCommand's capability-withdrawal guard: SupportsSessionTermination may only be turned
    -- off when no config on the target still opts into TerminateSessions.
    SELECT 1
    FROM [dbo].[PamRotationConfig]
    WHERE [TargetSystemId] = @TargetSystemId AND [TerminateSessions] = 1
END
