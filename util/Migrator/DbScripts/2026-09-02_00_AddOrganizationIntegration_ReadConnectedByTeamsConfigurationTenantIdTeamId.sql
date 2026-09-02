CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegration_ReadConnectedByTeamsConfigurationTenantIdTeamId]
    @TenantId NVARCHAR(200),
    @TeamId NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        OI.*
    FROM
        [dbo].[OrganizationIntegrationView] OI
    CROSS APPLY OPENJSON(OI.[Configuration], '$.Teams')
        WITH ([TeamId] NVARCHAR(MAX) '$.id') T
    WHERE
        OI.[Type] = 7
        AND JSON_VALUE(OI.[Configuration], '$.TenantId') = @TenantId
        AND T.[TeamId] = @TeamId
        AND JSON_VALUE(OI.[Configuration], '$.ChannelId') IS NOT NULL
        AND JSON_VALUE(OI.[Configuration], '$.ServiceUrl') IS NOT NULL
        AND JSON_VALUE(OI.[Configuration], '$.DisconnectedDate') IS NULL
END
GO
