CREATE PROCEDURE [dbo].[PamDaemonTargetAssignment_ExistsByDaemonIdTargetSystemId]
    @DaemonId UNIQUEIDENTIFIER,
    @TargetSystemId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT 1
    FROM [dbo].[PamDaemonTargetAssignment]
    WHERE [DaemonId] = @DaemonId AND [TargetSystemId] = @TargetSystemId
END
