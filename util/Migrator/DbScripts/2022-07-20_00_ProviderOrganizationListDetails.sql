-- Add columns 'UserCount', 'Seats' and 'Plan'
CREATE OR ALTER VIEW [dbo].[ProviderOrganizationOrganizationDetailsView]
AS
SELECT
    PO.[Id],
    PO.[ProviderId],
    PO.[OrganizationId],
    O.[Name] OrganizationName,
    PO.[Key],
    PO.[Settings],
    PO.[CreationDate],
    PO.[RevisionDate],
    (SELECT COUNT(1) FROM [dbo].[OrganizationUser] OU WHERE OU.OrganizationId = PO.OrganizationId) UserCount,
    O.Seats Seats,
    O.[Plan]
FROM
    [dbo].[ProviderOrganization] PO
LEFT JOIN
    [dbo].[Organization] O ON O.[Id] = PO.[OrganizationId]
GO
