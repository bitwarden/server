CREATE PROCEDURE [dbo].[Collection_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        S.*
    FROM
        [dbo].[CollectionView] S
    WHERE
        S.[OrganizationId] = @OrganizationId
END