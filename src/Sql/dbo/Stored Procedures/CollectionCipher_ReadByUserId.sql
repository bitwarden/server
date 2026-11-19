CREATE PROCEDURE [dbo].[CollectionCipher_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    ;WITH [UserCollections] AS (
        SELECT
            CU.[CollectionId]
        FROM
            [dbo].[OrganizationUser] OU
        INNER JOIN
            [dbo].[CollectionUser] CU ON CU.[OrganizationUserId] = OU.[Id]
        INNER JOIN
            [dbo].[Collection] S ON S.[Id] = CU.[CollectionId] AND S.[OrganizationId] = OU.[OrganizationId]
        WHERE
            OU.[UserId] = @UserId
            AND OU.[Status] = 2

        UNION

        SELECT
            CG.[CollectionId]
        FROM
            [dbo].[OrganizationUser] OU
        INNER JOIN
            [dbo].[GroupUser] GU ON GU.[OrganizationUserId] = OU.[Id]
        INNER JOIN
            [dbo].[CollectionGroup] CG ON CG.[GroupId] = GU.[GroupId]
        INNER JOIN
            [dbo].[Collection] S ON S.[Id] = CG.[CollectionId] AND S.[OrganizationId] = OU.[OrganizationId]
        WHERE
            OU.[UserId] = @UserId
            AND OU.[Status] = 2
    )
    SELECT
        CC.*
    FROM
        [dbo].[CollectionCipher] CC
    INNER JOIN
        [UserCollections] UC ON UC.[CollectionId] = CC.[CollectionId]
END
