CREATE VIEW [dbo].[CollectionUserCollectionDetailsView]
AS
SELECT
    SU.[Id],
    SU.[OrganizationUserId],
    S.[Name],
    S.[Id] CollectionId,
    SU.[ReadOnly]
FROM
    [dbo].[CollectionUser] SU
INNER JOIN
    [dbo].[Collection] S ON S.[Id] = SU.[CollectionId]
