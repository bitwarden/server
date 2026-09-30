CREATE PROCEDURE [dbo].[OrganizationIntegration_ReadManyConnectedByTeamsConfigurationTenantIdTeamId]
    @TenantId NVARCHAR(200),
    @TeamId NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        OI.*
    FROM
        [dbo].[OrganizationIntegrationView] OI
    WHERE
        OI.[Type] = 7
        AND JSON_VALUE(OI.[Configuration], '$.TenantId') = @TenantId
        AND EXISTS (
            SELECT 1
            FROM OPENJSON(OI.[Configuration], '$.Teams')
                WITH ([TeamId] NVARCHAR(MAX) '$.id') T
            WHERE T.[TeamId] = @TeamId
        )
        AND JSON_VALUE(OI.[Configuration], '$.ChannelId') IS NOT NULL
        AND JSON_VALUE(OI.[Configuration], '$.ServiceUrl') IS NOT NULL
        AND JSON_VALUE(OI.[Configuration], '$.DisconnectedDate') IS NULL
END
