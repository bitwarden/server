CREATE PROCEDURE [dbo].[OrganizationUser_ReadByUserIdWithPolicyDetails]
    @UserId UNIQUEIDENTIFIER,
    @PolicyType TINYINT
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
    CASE WHEN EXISTS (
            SELECT 1
            FROM [dbo].[ProviderUserView] PU
            INNER JOIN [dbo].[ProviderOrganizationView] PO ON PO.[ProviderId] = PU.[ProviderId]
            WHERE PU.[UserId] = OU.[UserId] AND PO.[OrganizationId] = P.[OrganizationId]
        ) THEN 1 ELSE 0 END AS IsProvider
FROM [dbo].[PolicyView] P
INNER JOIN [dbo].[OrganizationUserView] OU
    ON P.[OrganizationId] = OU.[OrganizationId]
WHERE P.[Type] = @PolicyType AND
    (
        (OU.[Status] != 0 AND OU.[UserId] = @UserId) -- OrgUsers who have accepted their invite and are linked to a UserId
        OR EXISTS (
            SELECT 1
            FROM [dbo].[UserView] U
            WHERE U.[Id] = @UserId AND OU.[Email] = U.[Email] AND OU.[Status] = 0 -- 'Invited' OrgUsers are not linked to a UserId yet, so we have to look up their email
        )
    )
END
GO