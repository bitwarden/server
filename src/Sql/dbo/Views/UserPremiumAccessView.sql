CREATE VIEW [dbo].[UserPremiumAccessView]
AS
SELECT DISTINCT 
    U.[Id] AS UserId,
    CASE 
        WHEN U.[Premium] = 1 THEN 1
        WHEN EXISTS (
            SELECT 1
            FROM [dbo].[OrganizationUser] OU
            INNER JOIN [dbo].[Organization] O ON OU.[OrganizationId] = O.[Id]
            WHERE OU.[UserId] = U.[Id]
                AND O.[UsersGetPremium] = 1
                AND O.[Enabled] = 1
        )
        THEN 1
        ELSE 0
    END AS HasPremiumAccess
FROM 
    [dbo].[User] U

