CREATE VIEW [dbo].[CollectionUserUserDetailsView]
AS
SELECT
    OU.[Id] AS [OrganizationUserId],
    OU.[OrganizationId],
    OU.[AccessAll],
    CU.[Id],
    CU.[CollectionId],
    U.[Name],
    ISNULL(U.[Email], OU.[Email]) Email,
    OU.[Status],
    OU.[Type],
    CASE
        WHEN OU.[AccessAll] = 0 AND CU.[ReadOnly] = 1 AND G.[AccessAll] = 0 AND CG.[ReadOnly] = 1 THEN 1
        ELSE 0
    END [ReadOnly]
FROM
    [dbo].[OrganizationUser] OU
LEFT JOIN
    [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[OrganizationUserId] = OU.[Id]
LEFT JOIN
    [dbo].[User] U ON U.[Id] = OU.[UserId]
LEFT JOIN
    [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
LEFT JOIN
    [dbo].[Group] G ON G.[Id] = GU.[GroupId]
LEFT JOIN
    [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[GroupId] = GU.[GroupId]