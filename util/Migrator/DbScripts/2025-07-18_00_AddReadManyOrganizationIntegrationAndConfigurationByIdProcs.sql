CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegration_ReadManyByOrganizationId]
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

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegrationConfiguration_ReadManyByOrganizationIntegrationId]
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
