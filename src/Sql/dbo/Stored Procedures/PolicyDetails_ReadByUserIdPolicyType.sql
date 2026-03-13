CREATE PROCEDURE [dbo].[PolicyDetails_ReadByUserIdPolicyType]
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
        -- Non-invited users (Status != 0): direct UserId match
        SELECT
            OU.[Id],
            OU.[OrganizationId],
            OU.[Type],
            OU.[Status],
            OU.[Permissions]
        FROM
            [dbo].[OrganizationUserView] OU
        WHERE
            OU.[Status] != 0
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
