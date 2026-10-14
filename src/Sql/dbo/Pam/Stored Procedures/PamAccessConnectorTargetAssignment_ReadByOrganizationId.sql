CREATE PROCEDURE [dbo].[PamAccessConnectorTargetAssignment_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[PamAccessConnectorTargetAssignmentView]
    WHERE
        [OrganizationId] = @OrganizationId
END
