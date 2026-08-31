CREATE PROCEDURE [dbo].[PamDaemonTargetAssignment_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PamDaemonTargetAssignment]
    WHERE [OrganizationId] = @OrganizationId
END
