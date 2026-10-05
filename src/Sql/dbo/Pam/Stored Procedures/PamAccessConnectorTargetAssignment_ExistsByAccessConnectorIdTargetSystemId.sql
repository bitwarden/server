CREATE PROCEDURE [dbo].[PamAccessConnectorTargetAssignment_ExistsByAccessConnectorIdTargetSystemId]
    @AccessConnectorId UNIQUEIDENTIFIER,
    @TargetSystemId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT 1
    FROM [dbo].[PamAccessConnectorTargetAssignment]
    WHERE [AccessConnectorId] = @AccessConnectorId AND [TargetSystemId] = @TargetSystemId
END
