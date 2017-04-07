CREATE VIEW [dbo].[SubvaultUserUserDetailsView]
AS
SELECT
    SU.[Id],
    SU.[OrganizationUserId],
    SU.[SubvaultId],
    U.[Name],
    ISNULL(U.[Email], OU.[Email]) Email,
    OU.[Status],
    OU.[Type],
    SU.[ReadOnly]
FROM
    [dbo].[SubvaultUser] SU
INNER JOIN
    [dbo].[OrganizationUser] OU ON OU.[Id] = SU.[OrganizationUserId]
LEFT JOIN
    [dbo].[User] U ON U.[Id] = OU.[UserId]