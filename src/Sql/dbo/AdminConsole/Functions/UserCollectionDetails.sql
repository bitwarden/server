CREATE FUNCTION [dbo].[UserCollectionDetails](@UserId UNIQUEIDENTIFIER)
RETURNS TABLE
AS RETURN
SELECT
    C.*,
    CASE
        WHEN
            COALESCE(CU.[ReadOnly], CG.[ReadOnly], 0) = 0
        THEN 0
        ELSE 1
    END [ReadOnly],
    CASE
        WHEN
            COALESCE(CU.[HidePasswords], CG.[HidePasswords], 0) = 0
        THEN 0
        ELSE 1
    END [HidePasswords],
    CASE
        WHEN
            COALESCE(CU.[Manage], CG.[Manage], 0) = 0
        THEN 0
        ELSE 1
    END [Manage]
FROM
    [dbo].[CollectionView] C
INNER JOIN
    [dbo].[OrganizationUser] OU ON C.[OrganizationId] = OU.[OrganizationId]
INNER JOIN
    [dbo].[Organization] O ON O.[Id] = C.[OrganizationId]
LEFT JOIN
    [dbo].[CollectionUser] CU ON CU.[CollectionId] = C.[Id] AND CU.[OrganizationUserId] = [OU].[Id]
LEFT JOIN
    [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND GU.[OrganizationUserId] = OU.[Id]
LEFT JOIN
    [dbo].[Group] G ON G.[Id] = GU.[GroupId]
LEFT JOIN
    [dbo].[CollectionGroup] CG ON CG.[CollectionId] = C.[Id] AND CG.[GroupId] = GU.[GroupId]
WHERE
    OU.[UserId] = @UserId
    AND OU.[Status] = 2 -- 2 = Confirmed
    AND O.[Enabled] = 1
    AND (
        CU.[CollectionId] IS NOT NULL
        OR CG.[CollectionId] IS NOT NULL
    )
