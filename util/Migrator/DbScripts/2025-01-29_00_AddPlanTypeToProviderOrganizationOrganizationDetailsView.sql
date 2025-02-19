-- Add column 'PlanType'
CREATE OR AlTER VIEW [dbo].[ProviderOrganizationOrganizationDetailsView]
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
    (SELECT COUNT(1) FROM [dbo].[OrganizationUser] OU WHERE OU.OrganizationId = PO.OrganizationId AND OU.Status = 2) UserCount,
    (SELECT COUNT(1) FROM [dbo].[OrganizationUser] OU WHERE OU.OrganizationId = PO.OrganizationId AND OU.Status >= 0) OccupiedSeats,
    O.[Seats],
    O.[Plan],
    O.[PlanType],
    O.[Status]
FROM
    [dbo].[ProviderOrganization] PO
        LEFT JOIN
    [dbo].[Organization] O ON O.[Id] = PO.[OrganizationId]
GO
