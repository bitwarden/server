CREATE VIEW [dbo].[CollectionUserUserDetailsView]
AS
SELECT
    OU.[Id] AS [OrganizationUserId],
    OU.[AccessAllCollections],
    SU.[Id],
    SU.[CollectionId],
    U.[Name],
    ISNULL(U.[Email], OU.[Email]) Email,
    OU.[Status],
    OU.[Type],
    CASE WHEN OU.[AccessAllCollections] = 0 AND SU.[ReadOnly] = 1 THEN 1 ELSE 0 END [ReadOnly]
FROM
    [dbo].[OrganizationUser] OU
LEFT JOIN
    [dbo].[CollectionUser] SU ON OU.[AccessAllCollections] = 0 AND SU.[OrganizationUserId] = OU.[Id]
LEFT JOIN
    [dbo].[User] U ON U.[Id] = OU.[UserId]