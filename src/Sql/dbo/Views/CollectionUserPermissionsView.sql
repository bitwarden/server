CREATE OR ALTER VIEW [dbo].[CollectionUserPermissionsView]
AS
SELECT
    CU.[OrganizationUserId],
    CU.[CollectionId],
    C.[OrganizationId],
    C.[Name] AS [CollectionName],
    CU.[ReadOnly],
    CU.[HidePasswords],
    CU.[Manage]
FROM
    [dbo].[CollectionUser] CU
        INNER JOIN
    [dbo].[Collection] C ON C.[Id] = CU.[CollectionId]
GO
