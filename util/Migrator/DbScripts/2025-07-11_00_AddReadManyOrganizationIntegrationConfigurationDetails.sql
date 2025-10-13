CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegrationConfigurationDetails_ReadMany]
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        oic.*
    FROM
        [dbo].[OrganizationIntegrationConfigurationDetailsView] oic
END
GO
