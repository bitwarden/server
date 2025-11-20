CREATE OR ALTER VIEW [dbo].[CollectionGroupPermissionsView]
AS
SELECT
    GU.[OrganizationUserId],
    G.[Id] AS [GroupId],
    G.[Name] AS [GroupName],
    G.[OrganizationId],
    CG.[CollectionId],
    C.[Name] AS [CollectionName],
    CG.[ReadOnly],
    CG.[HidePasswords],
    CG.[Manage]
FROM
    [dbo].[GroupUser] GU
        INNER JOIN
    [dbo].[Group] G ON G.[Id] = GU.[GroupId]
        INNER JOIN
    [dbo].[CollectionGroup] CG ON CG.[GroupId] = G.[Id]
        INNER JOIN
    [dbo].[Collection] C ON C.[Id] = CG.[CollectionId]
GO
