CREATE PROCEDURE [dbo].[Collection_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        C.*
    FROM
        [dbo].[CollectionView] C
    INNER JOIN
        [Organization] O ON O.[Id] = C.[OrganizationId]
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = O.[Id] AND OU.[UserId] = @UserId
    LEFT JOIN
        [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[CollectionId] = C.[Id] AND CU.[OrganizationUserId] = OU.[Id]
    WHERE
        OU.[Status] = 2 -- Confirmed
        AND O.[Enabled] = 1
        AND (OU.[AccessAll] = 1 OR CU.[CollectionId] IS NOT NULL)
END