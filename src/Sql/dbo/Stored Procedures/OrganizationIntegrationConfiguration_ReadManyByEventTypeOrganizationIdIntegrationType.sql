CREATE PROCEDURE [dbo].[OrganizationIntegrationConfiguration_ReadManyByEventTypeOrganizationIdIntegrationType]
    @EventType SMALLINT,
    @OrganizationId UNIQUEIDENTIFIER,
    @IntegrationType SMALLINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        oic.*
    FROM
        [dbo].[OrganizationIntegrationConfigurationView] oic
        INNER JOIN
        [dbo].[OrganizationIntegration] oi ON oi.[Id] = oic.[OrganizationIntegrationId]
    WHERE
        oic.[EventType] = @EventType
        AND
        oi.[OrganizationId] = @OrganizationId
        AND
        oi.[Type] = @IntegrationType
END
GO
