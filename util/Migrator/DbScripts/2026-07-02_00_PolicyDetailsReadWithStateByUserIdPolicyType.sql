CREATE OR ALTER PROCEDURE [dbo].[PolicyDetails_ReadWithStateByUserIdPolicyType]
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
        O.[Id] AS OrganizationId,
        @PolicyType AS PolicyType,
        P.[Data] AS PolicyData,
        OU.[Type] AS OrganizationUserType,
        OU.[Status] AS OrganizationUserStatus,
        OU.[Permissions] AS OrganizationUserPermissionsData,
        CASE WHEN PR.[OrganizationId] IS NULL THEN 0 ELSE 1 END AS IsProvider,
        -- Raw policy state: NULL when the organization has no row for this type
        P.[Enabled] AS [Enabled]
    FROM
        OrgUsers OU
    INNER JOIN
        [dbo].[OrganizationView] O ON OU.[OrganizationId] = O.[Id]
    LEFT JOIN
        [dbo].[PolicyView] P ON P.[OrganizationId] = O.[Id] AND P.[Type] = @PolicyType
    LEFT JOIN
        Providers PR ON PR.[OrganizationId] = OU.[OrganizationId]
    WHERE
        O.[Enabled] = 1
        AND O.[UsePolicies] = 1
END
GO
