CREATE PROCEDURE [dbo].[OrganizationIntegration_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

SELECT
    *
FROM
    [dbo].[OrganizationIntegrationView]
WHERE
    [OrganizationId] = @OrganizationId
END
