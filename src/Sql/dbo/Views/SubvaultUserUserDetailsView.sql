CREATE VIEW [dbo].[SubvaultUserUserDetailsView]
AS
SELECT
    OU.[Id] AS [OrganizationUserId],
    OU.[AccessAllSubvaults],
    SU.[Id],
    SU.[SubvaultId],
    U.[Name],
    ISNULL(U.[Email], OU.[Email]) Email,
    OU.[Status],
    OU.[Type],
    CASE WHEN OU.[AccessAllSubvaults] = 0 AND SU.[ReadOnly] = 1 THEN 1 ELSE 0 END [ReadOnly]
FROM
    [dbo].[OrganizationUser] OU
LEFT JOIN
    [dbo].[SubvaultUser] SU ON OU.[AccessAllSubvaults] = 0 AND SU.[OrganizationUserId] = OU.[Id]
LEFT JOIN
    [dbo].[User] U ON U.[Id] = OU.[UserId]