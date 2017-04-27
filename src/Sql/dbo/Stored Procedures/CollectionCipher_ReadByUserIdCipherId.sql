CREATE PROCEDURE [dbo].[CollectionCipher_ReadByUserIdCipherId]
    @UserId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER
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
        [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[CollectionId] = S.[Id] AND CU.[OrganizationUserId] = OU.[Id]
    WHERE
        SC.[CipherId] = @CipherId
        AND OU.[Status] = 2 -- Confirmed
        AND (OU.[AccessAll] = 1 OR CU.[CollectionId] IS NOT NULL)
END