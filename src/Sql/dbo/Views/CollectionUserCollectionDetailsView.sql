CREATE VIEW [dbo].[CollectionUserCollectionDetailsView]
AS
SELECT
    C.[Id] Id,
    C.[OrganizationId],
    C.[Name],
    OU.[UserId],
    OU.[Id] AS [OrganizationUserId],
    CASE WHEN OU.[AccessAll] = 0 AND CU.[ReadOnly] = 1 THEN 1 ELSE 0 END [ReadOnly]
FROM
    [dbo].[Collection] C
INNER JOIN
    [dbo].[OrganizationUser] OU ON C.[OrganizationId] = OU.[OrganizationId]
LEFT JOIN
    [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[CollectionId] = C.[Id] AND CU.[OrganizationUserId] = [OU].[Id]
WHERE
    OU.[AccessAll] = 1
    OR CU.[Id] IS NOT NULL