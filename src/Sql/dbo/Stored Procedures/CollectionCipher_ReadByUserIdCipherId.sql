CREATE PROCEDURE [dbo].[CollectionCipher_ReadByUserIdCipherId]
    @UserId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER
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
    LEFT JOIN
        [dbo].[CollectionUser] CU ON CU.[CollectionId] = S.[Id] AND CU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND GU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON CG.[CollectionId] = CC.[CollectionId] AND CG.[GroupId] = GU.[GroupId]
    WHERE
        CC.[CipherId] = @CipherId
        AND OU.[Status] = 2 -- Confirmed
        AND (
            CU.[CollectionId] IS NOT NULL
            OR CG.[CollectionId] IS NOT NULL
        )
END
