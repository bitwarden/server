CREATE PROCEDURE [dbo].[PamDaemonDetails_ReadByApiKeyId]
    @ApiKeyId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- The client provider's lookup at token-issuance time: the daemon row plus the two organization flags that gate
    -- issuance (Enabled, UsePam) so a lapsed/disabled org's daemon cannot mint a token without an extra round trip.
    SELECT
        D.*,
        O.[Enabled] AS [OrganizationEnabled],
        O.[UsePam] AS [OrganizationUsePam]
    FROM [dbo].[PamDaemon] D
    INNER JOIN [dbo].[Organization] O ON O.[Id] = D.[OrganizationId]
    WHERE D.[ApiKeyId] = @ApiKeyId
END
