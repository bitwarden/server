CREATE PROCEDURE [dbo].[PolicyDetails_ReadByUserIdsPolicyType]
    @UserIds AS [dbo].[GuidIdArray] READONLY,
    @PolicyType AS TINYINT
AS
BEGIN
    SET NOCOUNT ON;

    WITH AcceptedUsers AS (
        -- Branch 1: Accepted users linked by UserId
        SELECT
            OU.[Id]          AS OrganizationUserId,
            P.[OrganizationId],
            P.[Type]         AS PolicyType,
            P.[Data]         AS PolicyData,
            OU.[Type]        AS OrganizationUserType,
            OU.[Status]      AS OrganizationUserStatus,
            OU.[Permissions] AS OrganizationUserPermissionsData,
            OU.[UserId]      AS UserId
        FROM [dbo].[PolicyView] P
        INNER JOIN [dbo].[OrganizationUserView] OU ON P.[OrganizationId] = OU.[OrganizationId]
        INNER JOIN [dbo].[OrganizationView] O ON P.[OrganizationId] = O.[Id]
        INNER JOIN @UserIds UI ON OU.[UserId] = UI.Id -- Direct join with TVP
        WHERE
            P.Enabled = 1
            AND O.Enabled = 1
            AND O.UsePolicies = 1
            AND OU.[Status] != 0 -- Accepted users
            AND P.[Type] = @PolicyType
    ),
    InvitedUsers AS (
        -- Branch 2: Invited users matched by email
        SELECT
            OU.[Id]          AS OrganizationUserId,
            P.[OrganizationId],
            P.[Type]         AS PolicyType,
            P.[Data]         AS PolicyData,
            OU.[Type]        AS OrganizationUserType,
            OU.[Status]      AS OrganizationUserStatus,
            OU.[Permissions] AS OrganizationUserPermissionsData,
            OU.[UserId]      AS UserId
        FROM [dbo].[PolicyView] P
        INNER JOIN [dbo].[OrganizationUserView] OU ON P.[OrganizationId] = OU.[OrganizationId]
        INNER JOIN [dbo].[OrganizationView] O ON P.[OrganizationId] = O.[Id]
        INNER JOIN [dbo].[UserView] U ON U.[Email] = OU.[Email] -- Join on email
        INNER JOIN @UserIds UI ON U.[Id] = UI.Id -- Join with TVP
        WHERE
            P.Enabled = 1
            AND O.Enabled = 1
            AND O.UsePolicies = 1
            AND OU.[Status] = 0 -- Invited users only
            AND P.[Type] = @PolicyType
    )
    -- Combine results with IsProvider calculation
    SELECT
        OrganizationUserId,
        OrganizationId,
        PolicyType,
        PolicyData,
        OrganizationUserType,
        OrganizationUserStatus,
        OrganizationUserPermissionsData,
        UserId,
        IIF(
            EXISTS(
                SELECT 1
                FROM [dbo].[ProviderUserView] PU
                INNER JOIN [dbo].[ProviderOrganizationView] PO ON PO.[ProviderId] = PU.[ProviderId]
                WHERE PU.[UserId] = UserId
                AND PO.[OrganizationId] = OrganizationId
            ),
            1,
            0
        ) AS IsProvider
    FROM AcceptedUsers
    UNION
    SELECT
        OrganizationUserId,
        OrganizationId,
        PolicyType,
        PolicyData,
        OrganizationUserType,
        OrganizationUserStatus,
        OrganizationUserPermissionsData,
        UserId,
        IIF(
            EXISTS(
                SELECT 1
                FROM [dbo].[ProviderUserView] PU
                INNER JOIN [dbo].[ProviderOrganizationView] PO ON PO.[ProviderId] = PU.[ProviderId]
                WHERE
                    PU.[UserId] = UserId
                    AND PO.[OrganizationId] = OrganizationId
            ),
            1,
            0
        ) AS IsProvider
    FROM InvitedUsers
END
