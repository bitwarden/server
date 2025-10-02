CREATE PROCEDURE [dbo].[PolicyDetails_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
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
        SELECT
            OU.[Id],
            OU.[OrganizationId],
            OU.[Type],
            OU.[Status],
            OU.[Permissions]
        FROM
            [dbo].[OrganizationUserView] OU
        WHERE
            (OU.[Status] <> 0 AND OU.[UserId] = @UserId)
            OR (OU.[Status] = 0 AND OU.[Email] = @UserEmail AND @UserEmail IS NOT NULL)
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
        P.[Data] AS [PolicyData],
        OU.[Type] AS [OrganizationUserType],
        OU.[Status] AS [OrganizationUserStatus],
        OU.[Permissions] AS [OrganizationUserPermissionsData],
        CASE WHEN PR.[OrganizationId] IS NULL THEN 0 ELSE 1 END AS [IsProvider]
    FROM
        [dbo].[PolicyView] P
    INNER JOIN
        [dbo].[OrganizationView] O ON P.[OrganizationId] = O.[Id]
    INNER JOIN
        OrgUsers OU ON P.[OrganizationId] = OU.[OrganizationId]
    LEFT JOIN
        Providers PR ON PR.[OrganizationId] = OU.[OrganizationId]
    WHERE
        P.[Enabled] = 1
        AND O.[Enabled] = 1
        AND O.[UsePolicies] = 1
END
