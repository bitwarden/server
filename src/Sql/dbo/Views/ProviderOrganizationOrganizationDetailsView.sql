CREATE VIEW [dbo].[ProviderOrganizationOrganizationDetailsView]
AS
SELECT
    PO.[Id],
    PO.[ProviderId],
    PO.[OrganizationId],
    O.[Name] OrganizationName,
    PO.[Key],
    PO.[Settings],
    PO.[CreationDate],
    PO.[RevisionDate]
FROM
    [dbo].[ProviderOrganization] PO
LEFT JOIN
    [dbo].[Organization] O ON O.[Id] = PO.[OrganizationId]
