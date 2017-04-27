CREATE PROCEDURE [dbo].[CollectionCipher_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        SC.*
    FROM
        [dbo].[CollectionCipher] SC
    INNER JOIN
        [dbo].[Collection] S ON S.[Id] = SC.[CollectionId]
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = S.[OrganizationId] AND OU.[UserId] = @UserId
    LEFT JOIN
        [dbo].[CollectionUser] SU ON OU.[AccessAllCollections] = 0 AND SU.[CollectionId] = S.[Id] AND SU.[OrganizationUserId] = OU.[Id]
    WHERE
        OU.[Status] = 2 -- Confirmed
        AND (OU.[AccessAllCollections] = 1 OR SU.[CollectionId] IS NOT NULL)
END