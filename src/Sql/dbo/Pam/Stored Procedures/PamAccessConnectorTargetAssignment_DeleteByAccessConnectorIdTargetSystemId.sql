CREATE PROCEDURE [dbo].[PamAccessConnectorTargetAssignment_DeleteByAccessConnectorIdTargetSystemId]
    @AccessConnectorId UNIQUEIDENTIFIER,
    @TargetSystemId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE FROM [dbo].[PamAccessConnectorTargetAssignment]
    WHERE [AccessConnectorId] = @AccessConnectorId AND [TargetSystemId] = @TargetSystemId
END
