-- Restrict organization-sourced premium access to Confirmed members only.
-- Previously the view joined OrganizationUser without checking Status, so Revoked
-- (and other non-Confirmed) members continued to receive organization premium access.

CREATE OR ALTER VIEW [dbo].[UserPremiumAccessView]
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
    AND OU.[Status] = 2 -- Confirmed
LEFT JOIN
    [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId]
    AND O.[UsersGetPremium] = 1
    AND O.[Enabled] = 1
GROUP BY
    U.[Id], U.[Premium];
GO
