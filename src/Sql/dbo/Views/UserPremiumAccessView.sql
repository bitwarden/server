CREATE VIEW [dbo].[UserPremiumAccessView]
AS
SELECT 
    U.[Id],
    U.[Premium] AS [PersonalPremium],
    CAST(
        MAX(CASE 
            WHEN O.[Id] IS NOT NULL THEN 1 
            ELSE 0 
        END) AS BIT
    ) AS [OrganizationPremium]
FROM 
    [dbo].[User] U
LEFT JOIN 
    [dbo].[OrganizationUser] OU ON OU.[UserId] = U.[Id]
LEFT JOIN 
    [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId]
    AND O.[UsersGetPremium] = 1
    AND O.[Enabled] = 1
GROUP BY 
    U.[Id], U.[Premium];
