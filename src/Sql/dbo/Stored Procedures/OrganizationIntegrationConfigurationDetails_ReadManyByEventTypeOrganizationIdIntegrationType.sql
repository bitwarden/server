CREATE PROCEDURE [dbo].[OrganizationIntegrationConfigurationDetails_ReadManyByEventTypeOrganizationIdIntegrationType]
    @EventType SMALLINT,
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
        oic.[EventType] = @EventType
        AND
        oic.[OrganizationId] = @OrganizationId
        AND
        oic.[IntegrationType] = @IntegrationType
END
GO
