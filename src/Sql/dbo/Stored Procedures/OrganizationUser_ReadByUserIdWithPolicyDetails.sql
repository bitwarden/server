CREATE PROCEDURE [dbo].[OrganizationUser_ReadByUserIdWithPolicyDetails]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
SELECT
    P.[OrganizationId],
    P.[Type] as PolicyType,
    P.[Enabled] as PolicyEnabled,
    OU.[Type] as OrganizationUserType,
    OU.[Status] as OrganizationUserStatus,
    (CASE WHEN OU.[Permissions] IS NULL THEN 0 ELSE (CASE WHEN COALESCE(JSON_VALUE(OU.[Permissions], '$.managePolicies'), 'false') = 'false' THEN 0 ELSE 1 END) END) as CanManagePolicies,
    (CASE WHEN PUPO.[UserId] IS NULL THEN 0 ELSE 1 END) as IsProvider
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
    (
        (
            OU.[Status] != 0     -- OrgUsers who have accepted their invite and are linked to a UserId
            AND OU.[UserId] = @UserId
        )
       OR (
            OU.[Status] = 0     -- 'Invited' OrgUsers are not linked to a UserId yet, so we have to look up their email
            AND OU.[Email] IN (SELECT U.Email FROM [dbo].[UserView] U WHERE U.Id = @UserId)
        )
    )
END
