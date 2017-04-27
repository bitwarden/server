CREATE VIEW [dbo].[CollectionUserCollectionDetailsView]
AS
SELECT
    CU.[Id],
    CU.[OrganizationUserId],
    S.[Name],
    S.[Id] CollectionId,
    CU.[ReadOnly]
FROM
    [dbo].[CollectionUser] CU
INNER JOIN
    [dbo].[Collection] S ON S.[Id] = CU.[CollectionId]