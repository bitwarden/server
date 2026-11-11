-- Token-issuance lookup: carries the org's Enabled/UsePam flags, so a lapsed org can't mint a token.
CREATE VIEW [dbo].[PamAccessConnectorDetailsView]
AS
SELECT
    AC.[Id],
    AC.[OrganizationId],
    AC.[Name],
    AC.[ApiKeyId],
    AC.[Status],
    AC.[LastHeartbeatDate],
    AC.[CreationDate],
    AC.[RevisionDate],
    O.[Enabled] AS [OrganizationEnabled],
    O.[UsePam] AS [OrganizationUsePam]
FROM
    [dbo].[PamAccessConnector] AC
INNER JOIN
    [dbo].[Organization] O ON O.[Id] = AC.[OrganizationId]
