CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegration_ReadByTenantIdTeamId]
    @TenantId NVARCHAR(200),
    @TeamId NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

SELECT TOP 1
    [Id],
    [OrganizationId],
    [Type],
    [Configuration]
FROM [dbo].[OrganizationIntegrationView]
    CROSS APPLY OPENJSON([Configuration], '$.Teams')
    WITH ( TeamId NVARCHAR(MAX) '$.id' ) t
WHERE [Type] = 7
  AND JSON_VALUE([Configuration], '$.TenantId') = @TenantId
  AND t.TeamId = @TeamId
  AND JSON_VALUE([Configuration], '$.ChannelId') IS NULL
  AND JSON_VALUE([Configuration], '$.ServiceUrl') IS NULL;
END
GO
