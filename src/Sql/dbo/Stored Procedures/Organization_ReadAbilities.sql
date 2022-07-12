CREATE PROCEDURE [dbo].[Organization_ReadAbilities]
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        O.[Id],
        O.[UseEvents],
        O.[Use2fa],
        CASE
            WHEN [Use2fa] = 1 AND [TwoFactorProviders] IS NOT NULL AND [TwoFactorProviders] != '{}' THEN
                1
            ELSE
                0
            END AS [Using2fa],
        ISNULL(O.[UsersGetPremium], OPM.UsersGetPremium) AS UsersGetPremium,
        O.[UseSso],
        O.[UseKeyConnector],
        O.[UseResetPassword],
        O.[Enabled]
    FROM
         [dbo].[Organization] O
    LEFT JOIN OrganizationPasswordManager OPM on OPM.OrganizationId = O.Id
END
