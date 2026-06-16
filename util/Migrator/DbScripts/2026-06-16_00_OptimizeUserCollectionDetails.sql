CREATE OR ALTER FUNCTION [dbo].[UserCollectionDetails](@UserId UNIQUEIDENTIFIER)
RETURNS TABLE
AS RETURN
SELECT
    C.*,
    CASE WHEN COALESCE(CU.[ReadOnly], 0) = 0 THEN 0 ELSE 1 END [ReadOnly],
    CASE WHEN COALESCE(CU.[HidePasswords], 0) = 0 THEN 0 ELSE 1 END [HidePasswords],
    CASE WHEN COALESCE(CU.[Manage], 0) = 0 THEN 0 ELSE 1 END [Manage]
FROM
    [dbo].[CollectionView] C
INNER JOIN
    [dbo].[OrganizationUser] OU ON C.[OrganizationId] = OU.[OrganizationId]
INNER JOIN
    [dbo].[Organization] O ON O.[Id] = C.[OrganizationId]
INNER JOIN
    [dbo].[CollectionUser] CU ON CU.[CollectionId] = C.[Id] AND CU.[OrganizationUserId] = OU.[Id]
WHERE
    OU.[UserId] = @UserId
    AND OU.[Status] = 2 -- 2 = Confirmed
    AND O.[Enabled] = 1

UNION ALL

SELECT
    C.*,
    CASE WHEN COALESCE(CG.[ReadOnly], 0) = 0 THEN 0 ELSE 1 END [ReadOnly],
    CASE WHEN COALESCE(CG.[HidePasswords], 0) = 0 THEN 0 ELSE 1 END [HidePasswords],
    CASE WHEN COALESCE(CG.[Manage], 0) = 0 THEN 0 ELSE 1 END [Manage]
FROM
    [dbo].[CollectionView] C
INNER JOIN
    [dbo].[OrganizationUser] OU ON C.[OrganizationId] = OU.[OrganizationId]
INNER JOIN
    [dbo].[Organization] O ON O.[Id] = C.[OrganizationId]
INNER JOIN
    [dbo].[GroupUser] GU ON GU.[OrganizationUserId] = OU.[Id]
INNER JOIN
    [dbo].[CollectionGroup] CG ON CG.[CollectionId] = C.[Id] AND CG.[GroupId] = GU.[GroupId]
LEFT JOIN
    [dbo].[CollectionUser] CU ON CU.[CollectionId] = C.[Id] AND CU.[OrganizationUserId] = OU.[Id]
WHERE
    OU.[UserId] = @UserId
    AND OU.[Status] = 2 -- 2 = Confirmed
    AND O.[Enabled] = 1
    AND CU.[CollectionId] IS NULL
GO
