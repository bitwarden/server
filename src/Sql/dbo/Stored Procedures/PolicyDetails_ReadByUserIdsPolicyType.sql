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
                 U.[Id]           AS UserId
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
         ),
         AllUsers AS (
             -- Combine both user sets
             SELECT * FROM AcceptedUsers
             UNION
             SELECT * FROM InvitedUsers
         ),
         ProviderLookup AS (
             -- Pre-calculate provider relationships for all relevant user/org combinations
             SELECT DISTINCT
                 PU.[UserId],
                 PO.[OrganizationId]
             FROM [dbo].[ProviderUserView] PU
                      INNER JOIN [dbo].[ProviderOrganizationView] PO ON PO.[ProviderId] = PU.[ProviderId]
                      INNER JOIN AllUsers AU ON PU.[UserId] = AU.UserId AND PO.[OrganizationId] = AU.OrganizationId
         )
    -- Final result with efficient IsProvider lookup
    SELECT
        AU.OrganizationUserId,
        AU.OrganizationId,
        AU.PolicyType,
        AU.PolicyData,
        AU.OrganizationUserType,
        AU.OrganizationUserStatus,
        AU.OrganizationUserPermissionsData,
        AU.UserId,
        IIF(PL.UserId IS NOT NULL, 1, 0) AS IsProvider
    FROM AllUsers AU
             LEFT JOIN ProviderLookup PL
                       ON AU.UserId = PL.UserId
                           AND AU.OrganizationId = PL.OrganizationId
END
