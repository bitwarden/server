CREATE PROCEDURE [dbo].[PamDaemonDetails_ReadByApiKeyId]
    @ApiKeyId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Token-issuance lookup: includes the org's Enabled/UsePam flags.
    -- A lapsed org can't mint a token this way.
    SELECT
        D.*,
        O.[Enabled] AS [OrganizationEnabled],
        O.[UsePam] AS [OrganizationUsePam]
    FROM [dbo].[PamDaemon] D
    INNER JOIN [dbo].[Organization] O ON O.[Id] = D.[OrganizationId]
    WHERE D.[ApiKeyId] = @ApiKeyId
END
