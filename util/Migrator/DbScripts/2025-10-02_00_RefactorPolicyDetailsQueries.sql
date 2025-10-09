CREATE OR ALTER VIEW [dbo].[UserProviderAccessView]
AS
SELECT DISTINCT
    PU.[UserId],
    PO.[OrganizationId]
FROM
    [dbo].[ProviderUserView] PU
INNER JOIN
    [dbo].[ProviderOrganizationView] PO ON PO.[ProviderId] = PU.[ProviderId]
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_ReadByUserIdWithPolicyDetails]
    @UserId UNIQUEIDENTIFIER,
    @PolicyType TINYINT
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserEmail NVARCHAR(320)
    SELECT @UserEmail = Email
    FROM
        [dbo].[UserView]
    WHERE
        Id = @UserId

    ;WITH OrgUsers AS
    (
        -- All users except invited (Status <> 0): direct UserId match
        SELECT
            OU.[Id],
            OU.[OrganizationId],
            OU.[Type],
            OU.[Status],
            OU.[Permissions]
        FROM
            [dbo].[OrganizationUserView] OU
        WHERE
            OU.[Status] <> 0
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
