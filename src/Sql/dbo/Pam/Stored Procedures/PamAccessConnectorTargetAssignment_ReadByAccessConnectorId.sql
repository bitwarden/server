CREATE PROCEDURE [dbo].[PamAccessConnectorTargetAssignment_ReadByAccessConnectorId]
    @AccessConnectorId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[PamAccessConnectorTargetAssignmentView]
    WHERE
        [AccessConnectorId] = @AccessConnectorId
END
