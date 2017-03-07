CREATE VIEW [dbo].[OrganizationUserOrganizationDetailsView]
AS
SELECT
    OU.[UserId],
    OU.[OrganizationId],
    O.[Name],
    OU.[Key],
    OU.[Status]
FROM
    [dbo].[OrganizationUser] OU
INNER JOIN
    [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId]