CREATE FUNCTION [dbo].[UserCollectionDetails](@UserId UNIQUEIDENTIFIER)
RETURNS TABLE
AS RETURN
SELECT
    C.*,
    CASE
        WHEN
            OU.[AccessAll] = 1
            OR G.[AccessAll] = 1
            OR COALESCE(CU.[ReadOnly], CG.[ReadOnly], 0) = 0
        THEN 0
        ELSE 1
    END [ReadOnly],
    CASE
        WHEN
            OU.[AccessAll] = 1
            OR G.[AccessAll] = 1
            OR COALESCE(CU.[HidePasswords], CG.[HidePasswords], 0) = 0
        THEN 0
        ELSE 1
    END [HidePasswords]
FROM
    [dbo].[CollectionView] C
INNER JOIN
    [dbo].[OrganizationUser] OU ON C.[OrganizationId] = OU.[OrganizationId]
INNER JOIN
    [dbo].[Organization] O ON O.[Id] = C.[OrganizationId]
LEFT JOIN
    [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[CollectionId] = C.[Id] AND CU.[OrganizationUserId] = [OU].[Id]
LEFT JOIN
    [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
LEFT JOIN
    [dbo].[Group] G ON G.[Id] = GU.[GroupId]
LEFT JOIN
    [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[CollectionId] = C.[Id] AND CG.[GroupId] = GU.[GroupId]
WHERE
    OU.[UserId] = @UserId
    AND OU.[Status] = 2 -- 2 = Confirmed
    AND O.[Enabled] = 1
    AND (
        OU.[AccessAll] = 1
        OR CU.[CollectionId] IS NOT NULL
        OR G.[AccessAll] = 1
        OR CG.[CollectionId] IS NOT NULL
    )
