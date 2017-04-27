CREATE PROCEDURE [dbo].[Collection_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        S.*
    FROM
        [dbo].[CollectionView] S
    INNER JOIN
        [Organization] O ON O.[Id] = S.[OrganizationId]
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = O.[Id] AND OU.[UserId] = @UserId
    LEFT JOIN
        [dbo].[CollectionUser] SU ON OU.[AccessAllCollections] = 0 AND SU.[CollectionId] = S.[Id] AND SU.[OrganizationUserId] = OU.[Id]
    WHERE
        OU.[Status] = 2 -- Confirmed
        AND O.[Enabled] = 1
        AND (OU.[AccessAllCollections] = 1 OR SU.[CollectionId] IS NOT NULL)
END