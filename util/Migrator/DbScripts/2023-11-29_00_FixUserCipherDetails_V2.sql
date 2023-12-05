CREATE OR ALTER FUNCTION [dbo].[UserCipherDetails_V2](@UserId UNIQUEIDENTIFIER)
RETURNS TABLE
AS RETURN
WITH [CTE] AS (
    SELECT
        [Id],
        [OrganizationId]
    FROM
        [OrganizationUser]
    WHERE
        [UserId] = @UserId
        AND [Status] = 2 -- Confirmed
)
SELECT
    C.*,
    CASE
        WHEN COALESCE(CU.[ReadOnly], CG.[ReadOnly], 0) = 0
        THEN 1
        ELSE 0
    END [Edit],
    CASE
    	WHEN COALESCE(CU.[HidePasswords], CG.[HidePasswords], 0) = 0
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
    [dbo].[CollectionCipher] CC ON CC.[CipherId] = C.[Id]
LEFT JOIN
    [dbo].[CollectionUser] CU ON CU.[CollectionId] = CC.[CollectionId] AND CU.[OrganizationUserId] = OU.[Id]
LEFT JOIN
    [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND GU.[OrganizationUserId] = OU.[Id]
LEFT JOIN
    [dbo].[Group] G ON G.[Id] = GU.[GroupId]
LEFT JOIN
    [dbo].[CollectionGroup] CG ON CG.[CollectionId] = CC.[CollectionId] AND CG.[GroupId] = GU.[GroupId]
WHERE
    CU.[CollectionId] IS NOT NULL
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
GO
