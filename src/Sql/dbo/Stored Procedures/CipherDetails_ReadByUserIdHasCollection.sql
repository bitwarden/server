CREATE PROCEDURE [dbo].[CipherDetails_ReadByUserIdHasCollection]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        C.*,
        CASE 
            WHEN C.[UserId] IS NOT NULL OR OU.[AccessAll] = 1 OR CU.[ReadOnly] = 0 OR G.[AccessAll] = 1 OR CG.[ReadOnly] = 0 THEN 1
            ELSE 0
        END [Edit],
        CASE 
            WHEN C.[UserId] IS NULL AND O.[UseTotp] = 1 THEN 1
            ELSE 0
        END [OrganizationUseTotp]
    FROM
        [dbo].[CipherDetails](@UserId) C
    INNER JOIN
        [dbo].[Organization] O ON C.[UserId] IS NULL AND O.[Id] = C.[OrganizationId]
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = O.[Id] AND OU.[UserId] = @UserId
    LEFT JOIN
        [dbo].[CollectionCipher] CC ON C.[UserId] IS NULL AND OU.[AccessAll] = 0 AND CC.[CipherId] = C.[Id]
    LEFT JOIN
        [dbo].[CollectionUser] CU ON CU.[CollectionId] = CC.[CollectionId] AND CU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[GroupUser] GU ON C.[UserId] IS NULL AND CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[CollectionId] = CC.[CollectionId] AND CG.[GroupId] = GU.[GroupId]
    WHERE
        OU.[Status] = 2 -- 2 = Confirmed
        AND O.[Enabled] = 1
        AND (
            OU.[AccessAll] = 1
            OR CU.[CollectionId] IS NOT NULL
            OR G.[AccessAll] = 1
            OR CG.[CollectionId] IS NOT NULL
        )
END