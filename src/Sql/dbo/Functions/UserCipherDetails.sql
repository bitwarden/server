CREATE FUNCTION [dbo].[UserCipherDetails](@UserId UNIQUEIDENTIFIER)
RETURNS TABLE
AS RETURN
WITH [CTE] AS (
    SELECT
        [Id],
        [OrganizationId],
        [AccessAll]
    FROM
        [OrganizationUser]
    WHERE
        [UserId] = @UserId
        AND [Status] = 2 -- Confirmed
)
SELECT
    C.*,
    CASE
        WHEN
            OU.[AccessAll] = 1
            OR G.[AccessAll] = 1
            OR COALESCE(CU.[ReadOnly], CG.[ReadOnly], 0) = 0
        THEN 1
        ELSE 0
    END [Edit],
    CASE
        WHEN
            OU.[AccessAll] = 1
            OR G.[AccessAll] = 1
            OR COALESCE(CU.[HidePasswords], CG.[HidePasswords], 0) = 0
        THEN 1
        ELSE 0
    END [ViewPassword],
    CASE
        WHEN O.[UseTotp] = 1
        THEN 1
        ELSE 0
    END [OrganizationUseTotp]
FROM
    [dbo].[CipherDetails](@UserId) C
INNER JOIN
    [CTE] OU ON C.[UserId] IS NULL AND C.[OrganizationId] IN (SELECT [OrganizationId] FROM [CTE])
INNER JOIN
    [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId] AND O.[Id] = C.[OrganizationId] AND O.[Enabled] = 1
LEFT JOIN
    [dbo].[CollectionCipher] CC ON OU.[AccessAll] = 0 AND CC.[CipherId] = C.[Id]
LEFT JOIN
    [dbo].[CollectionUser] CU ON CU.[CollectionId] = CC.[CollectionId] AND CU.[OrganizationUserId] = OU.[Id]
LEFT JOIN
    [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
LEFT JOIN
    [dbo].[Group] G ON G.[Id] = GU.[GroupId]
LEFT JOIN
    [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[CollectionId] = CC.[CollectionId] AND CG.[GroupId] = GU.[GroupId]
WHERE
    OU.[AccessAll] = 1
    OR CU.[CollectionId] IS NOT NULL
    OR G.[AccessAll] = 1
    OR CG.[CollectionId] IS NOT NULL

UNION ALL

SELECT
    *,
    1 [Edit],
    1 [ViewPassword],
    0 [OrganizationUseTotp]
FROM
    [dbo].[CipherDetails](@UserId)
WHERE
    [UserId] = @UserId
