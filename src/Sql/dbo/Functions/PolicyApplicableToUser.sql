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
    (SELECT
        PU.UserId,
        PO.OrganizationId
    FROM
        [dbo].[ProviderUserView] PU
    INNER JOIN
        [ProviderOrganizationView] PO ON PO.[ProviderId] = PU.[ProviderId]) PUPO
    ON PUPO.UserId = OU.UserId
    AND PUPO.OrganizationId = P.OrganizationId
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
    AND PUPO.[UserId] IS NULL   -- Not a provider
