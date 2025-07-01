CREATE PROCEDURE [dbo].[PolicyDetails_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @PolicyType  TINYINT
AS
BEGIN
    SET NOCOUNT ON;

    WITH SelectedOrgUsers AS (
        SELECT
            OU.Id AS OrganizationUserID,
            U.Id AS UserId,
            U.Email
        FROM [dbo].[OrganizationView] AS O
        INNER JOIN [dbo].[OrganizationUserView] AS OU
            ON O.Id = OU.OrganizationId
        INNER JOIN [User] U
            ON (U.Email = OU.Email AND OU.[Status] = 0)
                OR OU.UserId = U.Id
        WHERE O.Id = @OrganizationId
    )

    SELECT OU.[Id] AS OrganizationUserId,
        SelectedOrgUsers.UserId,
        P.[OrganizationId],
        P.[Type]         AS PolicyType,
        P.[Data]         AS PolicyData,
        OU.[Type]        AS OrganizationUserType,
        OU.[Status]      AS OrganizationUserStatus,
        OU.[Permissions] AS OrganizationUserPermissionsData,
        CASE
            WHEN EXISTS (SELECT 1
                            FROM [dbo].[ProviderUserView] PU
                                    INNER JOIN [dbo].[ProviderOrganizationView] PO
                                                ON PO.[ProviderId] = PU.[ProviderId]
                            WHERE PU.[UserId] = OU.[UserId]
                            AND PO.[OrganizationId] = P.[OrganizationId]) THEN 1
            ELSE 0 END   AS IsProvider
    FROM [dbo].[PolicyView] P
            INNER JOIN [dbo].[OrganizationUserView] OU
                        ON P.[OrganizationId] = OU.[OrganizationId]
            INNER JOIN [dbo].[OrganizationView] O
                        ON P.[OrganizationId] = O.[Id]
            INNER JOIN SelectedOrgUsers
                        ON SelectedOrgUsers.UserId = OU.UserId
                            OR SelectedOrgUsers.Email = OU.Email
    WHERE P.Enabled = 1
    AND O.Enabled = 1
    AND O.UsePolicies = 1
    AND p.Type = @PolicyType

    END
GO
