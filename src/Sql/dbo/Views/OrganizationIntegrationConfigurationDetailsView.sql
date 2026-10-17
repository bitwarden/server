CREATE VIEW [dbo].[OrganizationIntegrationConfigurationDetailsView]
AS
    SELECT
        oic.[Id],
        oi.[Id] AS [OrganizationIntegrationId],
        oi.[OrganizationId],
        oi.[Type] AS [IntegrationType],
        oic.[EventType],
        oic.[Configuration],
        oi.[Configuration] AS [IntegrationConfiguration],
        oic.[Template],
        oic.[Filters],
        oic.[DisabledDate]
    FROM
        [dbo].[OrganizationIntegrationConfiguration] oic
        INNER JOIN
        [dbo].[OrganizationIntegration] oi ON oi.[Id] = oic.[OrganizationIntegrationId]
