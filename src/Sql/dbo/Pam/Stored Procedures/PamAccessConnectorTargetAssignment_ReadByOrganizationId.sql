CREATE PROCEDURE [dbo].[PamAccessConnectorTargetAssignment_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PamAccessConnectorTargetAssignment]
    WHERE [OrganizationId] = @OrganizationId
END
