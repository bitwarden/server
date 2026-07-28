CREATE OR ALTER PROCEDURE [dbo].[Organization_ReadOccupiedSeatCountByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        (
            -- Count organization users
            SELECT COUNT(1)
            FROM [dbo].[OrganizationUserView]
            WHERE OrganizationId = @OrganizationId
            AND Status IN (0, 1, 2) -- Invited, Accepted, Confirmed
        ) as Users,
        (
            -- Count admin-initiated sponsorships towards the seat count
            -- Introduced in https://bitwarden.atlassian.net/browse/PM-17772
            SELECT COUNT(1)
            FROM [dbo].[OrganizationSponsorship]
            WHERE SponsoringOrganizationId = @OrganizationId
            AND IsAdminInitiated = 1
            AND (
                -- Not marked for deletion - always count
                (ToDelete = 0)
                OR
                -- Marked for deletion but has a valid until date in the future (RevokeWhenExpired status)
                (ToDelete = 1 AND ValidUntil IS NOT NULL AND ValidUntil > GETUTCDATE())
            )
            AND (
                -- SENT status: When SponsoredOrganizationId is null
                SponsoredOrganizationId IS NULL
                OR
                -- ACCEPTED status: When SponsoredOrganizationId is not null and ValidUntil is null or in the future
                (SponsoredOrganizationId IS NOT NULL AND (ValidUntil IS NULL OR ValidUntil > GETUTCDATE()))
            )
        ) as Sponsored
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_ReadOccupiedSeatCountByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        (
            -- Count organization users
            SELECT COUNT(1)
            FROM [dbo].[OrganizationUserView]
            WHERE OrganizationId = @OrganizationId
            AND Status IN (0, 1, 2) -- Invited, Accepted, Confirmed
        ) +
        (
            -- Count admin-initiated sponsorships towards the seat count
            -- Introduced in https://bitwarden.atlassian.net/browse/PM-17772
            SELECT COUNT(1)
            FROM [dbo].[OrganizationSponsorship]
            WHERE SponsoringOrganizationId = @OrganizationId
            AND IsAdminInitiated = 1
            AND (
                -- Not marked for deletion - always count
                (ToDelete = 0)
                OR
                -- Marked for deletion but has a valid until date in the future (RevokeWhenExpired status)
                (ToDelete = 1 AND ValidUntil IS NOT NULL AND ValidUntil > GETUTCDATE())
            )
            AND (
                -- SENT status: When SponsoredOrganizationId is null
                SponsoredOrganizationId IS NULL
                OR
                -- ACCEPTED status: When SponsoredOrganizationId is not null and ValidUntil is null or in the future
                (SponsoredOrganizationId IS NOT NULL AND (ValidUntil IS NULL OR ValidUntil > GETUTCDATE()))
            )
        )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_ReadOccupiedSmSeatCountByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COUNT(1)
    FROM
        [dbo].[OrganizationUserView]
    WHERE
        OrganizationId = @OrganizationId
        AND Status IN (0, 1, 2) -- Invited, Accepted, Confirmed
        AND AccessSecretsManager = 1
END
GO

CREATE OR ALTER VIEW [dbo].[ProviderOrganizationOrganizationDetailsView]
AS
SELECT
    PO.[Id],
    PO.[ProviderId],
    PO.[OrganizationId],
    O.[Name] OrganizationName,
    PO.[Key],
    PO.[Settings],
    PO.[CreationDate],
    PO.[RevisionDate],
    (SELECT COUNT(1) FROM [dbo].[OrganizationUser] OU WHERE OU.OrganizationId = PO.OrganizationId AND OU.Status = 2) UserCount,
    (SELECT COUNT(1) FROM [dbo].[OrganizationUser] OU WHERE OU.OrganizationId = PO.OrganizationId AND OU.Status IN (0, 1, 2)) OccupiedSeats,
    O.[Seats],
    O.[Plan],
    O.[PlanType],
    O.[Status]
FROM
    [dbo].[ProviderOrganization] PO
LEFT JOIN
    [dbo].[Organization] O ON O.[Id] = PO.[OrganizationId]
GO

IF OBJECT_ID('[dbo].[ProviderOrganizationOrganizationDetails_ReadByProviderId]') IS NOT NULL
    EXEC sp_refreshsqlmodule '[dbo].[ProviderOrganizationOrganizationDetails_ReadByProviderId]'
GO

CREATE OR ALTER PROCEDURE [dbo].[PolicyDetails_ReadByUserIdsPolicyType]
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
          AND OU.[Status] IN (-1, 1, 2) -- Non-invited users
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
GO

CREATE OR ALTER PROCEDURE [dbo].[PolicyDetails_ReadByUserIdPolicyType]
    @UserId UNIQUEIDENTIFIER,
    @PolicyType TINYINT
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserEmail NVARCHAR(256)
    SELECT @UserEmail = Email
    FROM
        [dbo].[UserView]
    WHERE
        Id = @UserId

    ;WITH OrgUsers AS
    (
        -- Non-invited, non-staged users: direct UserId match
        SELECT
            OU.[Id],
            OU.[OrganizationId],
            OU.[Type],
            OU.[Status],
            OU.[Permissions]
        FROM
            [dbo].[OrganizationUserView] OU
        WHERE
            OU.[Status] IN (-1, 1, 2)
            AND OU.[UserId] = @UserId

        UNION ALL

        -- Invited users (Status = 0): email match
        SELECT
            OU.[Id],
            OU.[OrganizationId],
            OU.[Type],
            OU.[Status],
            OU.[Permissions]
        FROM
            [dbo].[OrganizationUserView] OU
        WHERE
            OU.[Status] = 0
            AND OU.[Email] = @UserEmail
            AND @UserEmail IS NOT NULL
    ),
    Providers AS
    (
        SELECT
            OrganizationId
        FROM
            [dbo].[UserProviderAccessView]
        WHERE
            UserId = @UserId
    )
    SELECT
        OU.[Id] AS OrganizationUserId,
        P.[OrganizationId],
        P.[Type] AS PolicyType,
        P.[Data] AS PolicyData,
        OU.[Type] AS OrganizationUserType,
        OU.[Status] AS OrganizationUserStatus,
        OU.[Permissions] AS OrganizationUserPermissionsData,
        CASE WHEN PR.[OrganizationId] IS NULL THEN 0 ELSE 1 END AS IsProvider
    FROM
        [dbo].[PolicyView] P
    INNER JOIN
        OrgUsers OU ON P.[OrganizationId] = OU.[OrganizationId]
    INNER JOIN
        [dbo].[OrganizationView] O ON P.[OrganizationId] = O.[Id]
    LEFT JOIN
        Providers PR ON PR.[OrganizationId] = OU.[OrganizationId]
    WHERE
        P.[Type] = @PolicyType
        AND P.[Enabled] = 1
        AND O.[Enabled] = 1
        AND O.[UsePolicies] = 1
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PolicyDetails_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
SELECT
    OU.[Id] AS OrganizationUserId,
    P.[OrganizationId],
    P.[Type] AS PolicyType,
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
INNER JOIN [dbo].[OrganizationView] O
    ON P.[OrganizationId] = O.[Id]
WHERE
    P.Enabled = 1
    AND O.Enabled = 1
    AND O.UsePolicies = 1
    AND (
            -- OrgUsers who have accepted their invite and are linked to a UserId
            -- (Note: this excludes "invited but revoked" users who don't have an OU.UserId yet,
            -- but those users will go through policy enforcement later as part of accepting their invite after being restored.
            -- This is an intentionally unhandled edge case for now.)
            (OU.[Status] IN (-1, 1, 2) AND OU.[UserId] = @UserId)

            -- 'Invited' OrgUsers are not linked to a UserId yet, so we have to look up their email
            OR EXISTS (
                SELECT 1
                FROM [dbo].[UserView] U
                WHERE U.[Id] = @UserId AND OU.[Email] = U.[Email] AND OU.[Status] = 0
            )
        )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_ReadByUserIdWithPolicyDetails]
    @UserId UNIQUEIDENTIFIER,
    @PolicyType TINYINT
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserEmail NVARCHAR(256)
    SELECT @UserEmail = Email
    FROM
        [dbo].[UserView]
    WHERE
        Id = @UserId

    ;WITH OrgUsers AS
    (
        -- All users except invited and staged: direct UserId match
        SELECT
            OU.[Id],
            OU.[OrganizationId],
            OU.[Type],
            OU.[Status],
            OU.[Permissions]
        FROM
            [dbo].[OrganizationUserView] OU
        WHERE
            OU.[Status] IN (-1, 1, 2)
            AND OU.[UserId] = @UserId

        UNION ALL

        -- Invited users: email match
        SELECT
            OU.[Id],
            OU.[OrganizationId],
            OU.[Type],
            OU.[Status],
            OU.[Permissions]
        FROM
            [dbo].[OrganizationUserView] OU
        WHERE
            OU.[Status] = 0
            AND OU.[Email] = @UserEmail
            AND @UserEmail IS NOT NULL
    ),
    Providers AS
    (
        SELECT
            OrganizationId
        FROM
            [dbo].[UserProviderAccessView]
        WHERE
            UserId = @UserId
    )
    SELECT
        OU.[Id] AS [OrganizationUserId],
        P.[OrganizationId],
        P.[Type] AS [PolicyType],
        P.[Enabled] AS [PolicyEnabled],
        P.[Data] AS [PolicyData],
        OU.[Type] AS [OrganizationUserType],
        OU.[Status] AS [OrganizationUserStatus],
        OU.[Permissions] AS [OrganizationUserPermissionsData],
        CASE WHEN PR.[OrganizationId] IS NULL THEN 0 ELSE 1 END AS [IsProvider]
    FROM
        [dbo].[PolicyView] P
    INNER JOIN
        OrgUsers OU ON P.[OrganizationId] = OU.[OrganizationId]
    LEFT JOIN
        Providers PR ON PR.[OrganizationId] = OU.[OrganizationId]
    WHERE
        P.[Type] = @PolicyType
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Organization_ReadByClaimedUserEmailDomain]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    WITH CTE_User AS (
        SELECT
            U.[Id],
            SUBSTRING(U.Email, CHARINDEX('@', U.Email) + 1, LEN(U.Email)) AS EmailDomain
        FROM dbo.[UserView] U
        WHERE U.[Id] = @UserId
    )
    SELECT O.*
    FROM CTE_User CU
             INNER JOIN dbo.[OrganizationUserView] OU ON CU.[Id] = OU.[UserId]
             INNER JOIN dbo.[OrganizationView] O ON OU.[OrganizationId] = O.[Id]
             INNER JOIN dbo.[OrganizationDomainView] OD ON OU.[OrganizationId] = OD.[OrganizationId]
    WHERE OD.[VerifiedDate] IS NOT NULL
      AND CU.EmailDomain = OD.[DomainName]
      AND O.[Enabled] = 1
      AND OU.[Status] IN (-1, 1, 2) -- Exclude invited and staged users
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_ReadByOrganizationIdWithClaimedDomains_V2]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    WITH OrgUsers AS (
        SELECT *
        FROM [dbo].[OrganizationUserView]
        WHERE [OrganizationId] = @OrganizationId
            AND [Status] IN (-1, 1, 2)   -- Exclude invited and staged users
    ),
    UserDomains AS (
        SELECT U.[Id], U.[EmailDomain]
        FROM [dbo].[UserEmailDomainView] U
        WHERE EXISTS (
            SELECT 1
            FROM [dbo].[OrganizationDomainView] OD
            WHERE OD.[OrganizationId] = @OrganizationId
            AND OD.[VerifiedDate] IS NOT NULL
            AND OD.[DomainName] = U.[EmailDomain]
        )
    )
    SELECT OU.*
    FROM OrgUsers OU
    JOIN UserDomains UD ON OU.[UserId] = UD.[Id]
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE dbo.MemberAccessReport_GetMemberAccessCipherDetailsByOrganizationId
    @OrganizationId UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

IF @OrganizationId IS NULL
        THROW 50000, 'OrganizationId cannot be null', 1;

    SELECT
        OU.Id AS UserGuid,
        U.Name AS UserName,
        ISNULL(U.Email, OU.Email) as 'Email',
        U.TwoFactorProviders,
        U.UsesKeyConnector,
        OU.ResetPasswordKey,
        CC.CollectionId,
        C.Name AS CollectionName,
        NULL AS GroupId,
        NULL AS GroupName,
        CU.ReadOnly,
        CU.HidePasswords,
        CU.Manage,
        Cipher.Id AS CipherId
    FROM dbo.OrganizationUser OU
            LEFT JOIN dbo.[User] U ON U.Id = OU.UserId
        INNER JOIN dbo.Organization O ON O.Id = OU.OrganizationId
        AND O.Id = @OrganizationId
        AND O.Enabled = 1
        INNER JOIN dbo.CollectionUser CU ON CU.OrganizationUserId = OU.Id
        INNER JOIN dbo.Collection C ON C.Id = CU.CollectionId and C.OrganizationId = @OrganizationId
        INNER JOIN dbo.CollectionCipher CC ON CC.CollectionId = C.Id
        INNER JOIN dbo.Cipher Cipher ON Cipher.Id = CC.CipherId AND Cipher.OrganizationId = @OrganizationId
    WHERE OU.Status IN (0,1,2,3) -- Invited, Accepted, Confirmed, and Staged Users
        AND Cipher.DeletedDate IS NULL
UNION ALL
    -- Group-based collection permissions
    SELECT
        OU.Id AS UserGuid,
        U.Name AS UserName,
        ISNULL(U.Email, OU.Email) as 'Email',
        U.TwoFactorProviders,
        U.UsesKeyConnector,
        OU.ResetPasswordKey,
        CC.CollectionId,
        C.Name AS CollectionName,
        G.Id AS GroupId,
        G.Name AS GroupName,
        CG.ReadOnly,
        CG.HidePasswords,
        CG.Manage,
        Cipher.Id AS CipherId
    FROM dbo.OrganizationUser OU
        LEFT JOIN dbo.[User] U ON U.Id = OU.UserId
        INNER JOIN dbo.Organization O ON O.Id = OU.OrganizationId
        AND O.Id = @OrganizationId
        AND O.Enabled = 1
        INNER JOIN dbo.GroupUser GU ON GU.OrganizationUserId = OU.Id
        INNER JOIN dbo.[Group] G ON G.Id = GU.GroupId
        INNER JOIN dbo.CollectionGroup CG ON CG.GroupId = G.Id
        INNER JOIN dbo.Collection C ON C.Id = CG.CollectionId AND C.OrganizationId = @OrganizationId
        INNER JOIN dbo.CollectionCipher CC ON CC.CollectionId = C.Id
        INNER JOIN dbo.Cipher Cipher ON Cipher.Id = CC.CipherId and Cipher.OrganizationId = @OrganizationId
    WHERE OU.Status IN (0,1,2,3)  -- Invited, Accepted, Confirmed, and Staged Users
        AND Cipher.DeletedDate IS NULL
UNION ALL
    -- Users without collection access (invited users)
    -- typically invited users who have not yet accepted the invitation
    -- and not yet assigned to any collection
    SELECT
        OU.Id AS UserGuid,
        U.Name AS UserName,
        ISNULL(U.Email, OU.Email) as 'Email',
        U.TwoFactorProviders,
        U.UsesKeyConnector,
        OU.ResetPasswordKey,
        null as CollectionId,
        null AS CollectionName,
        NULL AS GroupId,
        NULL AS GroupName,
        null as [ReadOnly],
        null as HidePasswords,
        null as Manage,
        null  AS CipherId
    FROM dbo.OrganizationUser OU
            LEFT JOIN dbo.[User] U ON U.Id = OU.UserId
        INNER JOIN dbo.Organization O ON O.Id = OU.OrganizationId AND O.Id = @OrganizationId AND O.Enabled = 1
    WHERE OU.Status IN (0,1,2,3)  -- Invited, Accepted, Confirmed, and Staged Users
        AND OU.Id not in (
            select OU1.Id from dbo.OrganizationUser OU1
                inner join dbo.CollectionUser CU1 on CU1.OrganizationUserId = OU1.Id
            WHERE OU1.OrganizationId = @organizationId
        )
GO
