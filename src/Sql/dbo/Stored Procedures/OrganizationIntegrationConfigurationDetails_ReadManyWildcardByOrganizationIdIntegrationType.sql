CREATE PROCEDURE [dbo].[OrganizationIntegrationConfigurationDetails_ReadManyWildcardByOrganizationIdIntegrationType]
    @OrganizationId UNIQUEIDENTIFIER,
    @IntegrationType SMALLINT
AS
BEGIN
    SET NOCOUNT ON

SELECT
    oic.*
FROM
    [dbo].[OrganizationIntegrationConfigurationDetailsView] oic
WHERE
    oic.[EventType] IS NULL
  AND
    oic.[OrganizationId] = @OrganizationId
  AND
    oic.[IntegrationType] = @IntegrationType
END
GO
