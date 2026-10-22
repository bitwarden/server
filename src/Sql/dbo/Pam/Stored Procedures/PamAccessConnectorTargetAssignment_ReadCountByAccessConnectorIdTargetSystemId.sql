CREATE PROCEDURE [dbo].[PamAccessConnectorTargetAssignment_ReadCountByAccessConnectorIdTargetSystemId]
    @AccessConnectorId UNIQUEIDENTIFIER,
    @TargetSystemId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- OneAssignmentPerConnectorTarget: the count is 0 or 1.
    SELECT
        COUNT(1)
    FROM
        [dbo].[PamAccessConnectorTargetAssignmentView]
    WHERE
        [AccessConnectorId] = @AccessConnectorId
        AND [TargetSystemId] = @TargetSystemId
END
