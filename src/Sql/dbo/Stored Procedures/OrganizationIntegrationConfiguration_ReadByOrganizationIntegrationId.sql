CREATE PROCEDURE [dbo].[OrganizationIntegrationConfiguration_ReadByOrganizationIntegrationId]
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
