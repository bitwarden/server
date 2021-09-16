CREATE FUNCTION [dbo].[PolicyApplicableToUser]
(
    @UserId UNIQUEIDENTIFIER,
    @PolicyType TINYINT,
    @MinimumStatus TINYINT
)
RETURNS TABLE
AS RETURN
SELECT
    P.*
FROM
    [dbo].[PolicyView] P
INNER JOIN
    [dbo].[OrganizationUserView] OU ON P.[OrganizationId] = OU.[OrganizationId]
LEFT JOIN
    [dbo].[ProviderUserProviderOrganizationDetailsView] PUPO 
        ON OU.[OrganizationId] = PUPO.[OrganizationId]
        AND OU.[UserId] = PUPO.[UserId]
WHERE
    OU.[UserId] = @UserId 
    AND P.[Type] = @PolicyType
    AND P.[Enabled] = 1
    AND OU.[Status] >= @MinimumStatus
    AND OU.[Type] >= 2              -- Not an owner (0) or admin (1)
    AND (                           -- Can't manage policies
        OU.[Permissions] IS NULL
        OR COALESCE(JSON_VALUE(OU.[Permissions], '$.managePolicies'), 'false') = 'false'
    )
    AND PUPO.[ProviderId] IS NULL   -- Not a provider
GO
