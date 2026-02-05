CREATE PROCEDURE [dbo].[OrganizationIntegrationConfiguration_ReadManyByOrganizationIntegrationId]
    @OrganizationIntegrationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

SELECT
    *
FROM
    [dbo].[OrganizationIntegrationConfigurationView]
WHERE
    [OrganizationIntegrationId] = @OrganizationIntegrationId
END
