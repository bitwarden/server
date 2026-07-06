CREATE PROCEDURE [dbo].[PamDaemonTargetAssignment_ReadByDaemonId]
    @DaemonId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PamDaemonTargetAssignment]
    WHERE [DaemonId] = @DaemonId
END
