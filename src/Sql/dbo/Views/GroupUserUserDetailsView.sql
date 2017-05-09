CREATE VIEW [dbo].[GroupUserUserDetailsView]
AS
SELECT
    OU.[Id] AS [OrganizationUserId],
    OU.[OrganizationId],
    OU.[AccessAll],
    GU.[GroupId],
    U.[Name],
    ISNULL(U.[Email], OU.[Email]) Email,
    OU.[Status],
    OU.[Type]
FROM
    [dbo].[OrganizationUser] OU
INNER JOIN
    [dbo].[GroupUser] GU ON GU.[OrganizationUserId] = OU.[Id]
INNER JOIN
    [dbo].[User] U ON U.[Id] = OU.[UserId]