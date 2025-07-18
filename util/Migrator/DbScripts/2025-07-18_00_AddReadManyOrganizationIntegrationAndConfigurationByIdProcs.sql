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
GO

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
GO
