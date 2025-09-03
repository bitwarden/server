IF EXISTS(SELECT *
FROM sys.views
WHERE [Name] = 'OrganizationIntegrationConfigurationDetailsView')
BEGIN
    DROP VIEW [dbo].[OrganizationIntegrationConfigurationDetailsView];
END
GO

CREATE VIEW [dbo].[OrganizationIntegrationConfigurationDetailsView]
AS
    SELECT
        oi.[OrganizationId],
        oi.[Type] AS [IntegrationType],
        oic.[EventType],
        oic.[Configuration],
        oi.[Configuration] AS [IntegrationConfiguration],
        oic.[Template]
    FROM
        [dbo].[OrganizationIntegrationConfiguration] oic
        INNER JOIN
        [dbo].[OrganizationIntegration] oi ON oi.[Id] = oic.[OrganizationIntegrationId]
GO

IF OBJECT_ID('[dbo].[OrganizationIntegrationConfiguration_ReadManyByEventTypeOrganizationIdIntegrationType]') IS NOT NULL
    BEGIN
    DROP PROCEDURE [dbo].[OrganizationIntegrationConfiguration_ReadManyByEventTypeOrganizationIdIntegrationType]
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegrationConfigurationDetails_ReadManyByEventTypeOrganizationIdIntegrationType]
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
