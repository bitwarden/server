IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'OrganizationUserUserDetailsView')
BEGIN
    DROP VIEW [dbo].[OrganizationUserUserDetailsView]
END
GO

CREATE VIEW [dbo].[OrganizationUserUserDetailsView]
AS
SELECT
    OU.[Id],
    OU.[UserId],
    OU.[OrganizationId],
    U.[Name],
    ISNULL(U.[Email], OU.[Email]) Email,
    U.[TwoFactorProviders],
    U.[Premium],
    OU.[Status],
    OU.[Type],
    OU.[AccessAll],
    OU.[ExternalId]
FROM
    [dbo].[OrganizationUser] OU
LEFT JOIN
    [dbo].[User] U ON U.[Id] = OU.[UserId]
GO
