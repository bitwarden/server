CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_ReadByUserIdWithPolicyDetails]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
SELECT
    OU.[Id] AS OrganizationUserId,
    P.[OrganizationId],
    P.[Type] AS PolicyType,
    P.[Enabled] AS PolicyEnabled,
    P.[Data] AS PolicyData,
    OU.[Type] AS OrganizationUserType,
    OU.[Status] AS OrganizationUserStatus,
    OU.[Permissions] AS OrganizationUserPermissionsData,
    CASE WHEN PU.[ProviderId] IS NOT NULL THEN 1 ELSE 0 END AS IsProvider
FROM [dbo].[PolicyView] P
INNER JOIN [dbo].[OrganizationUserView] OU
    ON P.[OrganizationId] = OU.[OrganizationId]
LEFT JOIN [dbo].[ProviderUserView] PU
    ON PU.[UserId] = OU.[UserId]
LEFT JOIN [dbo].[ProviderOrganizationView] PO
    ON PO.[ProviderId] = PU.[ProviderId] AND PO.[OrganizationId] = P.[OrganizationId]
WHERE
    (OU.[Status] != 0 AND OU.[UserId] = @UserId) -- OrgUsers who have accepted their invite and are linked to a UserId
    OR EXISTS (
        SELECT 1
        FROM [dbo].[UserView] U
        WHERE U.[Id] = @UserId AND OU.[Email] = U.[Email] AND OU.[Status] = 0 -- 'Invited' OrgUsers are not linked to a UserId yet, so we have to look up their email
    )
END
GO