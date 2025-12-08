CREATE VIEW [dbo].[UserPremiumAccessView]
AS
SELECT
    U.[Id],
    U.[Premium] AS [PersonalPremium],
    CAST(CASE 
        WHEN EXISTS (
            SELECT 1
            FROM [dbo].[OrganizationUser] OU
            INNER JOIN [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId]
            WHERE OU.[UserId] = U.[Id]
                AND O.[UsersGetPremium] = 1
                AND O.[Enabled] = 1
        ) THEN 1 
        ELSE 0 
    END AS BIT) AS [OrganizationPremium]
FROM
    [dbo].[User] U;
