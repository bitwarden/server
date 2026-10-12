CREATE PROCEDURE [dbo].[PamAccessConnectorTargetAssignment_ReadByAccessConnectorId]
    @AccessConnectorId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PamAccessConnectorTargetAssignment]
    WHERE [AccessConnectorId] = @AccessConnectorId
END
