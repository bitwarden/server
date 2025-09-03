CREATE PROCEDURE [dbo].[CollectionCipher_ReadSharedByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        CC.*
    FROM
        [dbo].[CollectionCipher] CC
    INNER JOIN
        [dbo].[Collection] C ON C.[Id] = CC.[CollectionId]
    WHERE
        C.[OrganizationId] = @OrganizationId
        AND C.[Type] = 0 -- SharedCollections only
END
