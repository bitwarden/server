CREATE PROCEDURE [dbo].[PamDaemonTargetAssignment_DeleteByDaemonIdTargetSystemId]
    @DaemonId UNIQUEIDENTIFIER,
    @TargetSystemId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE FROM [dbo].[PamDaemonTargetAssignment]
    WHERE [DaemonId] = @DaemonId AND [TargetSystemId] = @TargetSystemId
END
