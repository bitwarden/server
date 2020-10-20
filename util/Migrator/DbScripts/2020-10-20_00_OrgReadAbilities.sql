IF OBJECT_ID('[dbo].[Organization_ReadAbilities]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_ReadAbilities]
END
GO

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
        [Enabled]
    FROM
        [dbo].[Organization]
END
GO
