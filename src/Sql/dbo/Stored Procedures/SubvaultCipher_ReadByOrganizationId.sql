CREATE PROCEDURE [dbo].[CollectionCipher_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        SC.*
    FROM
        [dbo].[CollectionCipher] SC
    INNER JOIN
        [dbo].[Collection] S ON S.[Id] = SC.[CollectionId]
    WHERE
        S.[OrganizationId] = @OrganizationId
END