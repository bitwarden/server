CREATE PROCEDURE [dbo].[Collection_ReadSharedCollectionsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CollectionView]
    WHERE
        [OrganizationId] = @OrganizationId AND
        [Type] = 0 -- DefaultUserCollection only
END