CREATE PROCEDURE [dbo].[Organization_ReadAbilities]
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Id],
        [UseEvents],
        [Use2fa],
        CASE
        WHEN [Use2fa] = 1 AND [TwoFactorProviders] IS NOT NULL AND [TwoFactorProviders] != '{}' THEN
            1
        ELSE
            0
        END AS [Using2fa],
        [UsersGetPremium],
        [UseSso],
        [UseKeyConnector],
        [UseScim],
        [UseResetPassword],
        [Enabled]
    FROM
        [dbo].[Organization]
END
