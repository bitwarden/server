CREATE OR ALTER PROCEDURE [dbo].[CollectionCipher_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        CC.*
    FROM
        [dbo].[CollectionCipher] CC
    INNER JOIN
        [dbo].[Collection] S ON S.[Id] = CC.[CollectionId]
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = S.[OrganizationId] AND OU.[UserId] = @UserId
    INNER JOIN
        [dbo].[CollectionUser] CU ON CU.[CollectionId] = S.[Id] AND CU.[OrganizationUserId] = OU.[Id]
    WHERE
        OU.[Status] = 2

    UNION ALL

    SELECT 
        CC.*
    FROM
        [dbo].[CollectionCipher] CC
    INNER JOIN
        [dbo].[Collection] S ON S.[Id] = CC.[CollectionId]
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = S.[OrganizationId] AND OU.[UserId] = @UserId
    INNER JOIN
        [dbo].[GroupUser] GU ON GU.[OrganizationUserId] = OU.[Id]
    INNER JOIN
        [dbo].[CollectionGroup] CG ON CG.[CollectionId] = CC.[CollectionId] AND CG.[GroupId] = GU.[GroupId]
    LEFT JOIN
        [dbo].[CollectionUser] CU ON CU.[CollectionId] = S.[Id] AND CU.[OrganizationUserId] = OU.[Id]
    WHERE
        OU.[Status] = 2
        AND CU.[CollectionId] IS NULL
END
