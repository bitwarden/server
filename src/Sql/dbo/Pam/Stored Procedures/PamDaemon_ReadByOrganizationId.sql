CREATE PROCEDURE [dbo].[PamDaemon_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PamDaemon]
    WHERE [OrganizationId] = @OrganizationId
END
