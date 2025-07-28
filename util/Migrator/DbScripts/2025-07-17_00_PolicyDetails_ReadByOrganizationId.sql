CREATE OR ALTER PROCEDURE [dbo].[PolicyDetails_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @PolicyType  TINYINT
AS
BEGIN
     SET NOCOUNT ON;

    ;WITH GivenOrgUsers AS (
        SELECT
                OU.[UserId],
                U.[Email]
         FROM [dbo].[OrganizationUserView] OU
            INNER JOIN [dbo].[UserView] U ON U.[Id] = OU.[UserId]
         WHERE OU.[OrganizationId] = @OrganizationId

         UNION ALL

        SELECT
            OU.[Id] AS [OrganizationUserId],
                    U.[Id] AS [UserId],
                    U.[Email]
        FROM [dbo].[OrganizationUserView] OU
            INNER JOIN [dbo].[UserView] U ON U.[Email] = OU.[Email]
        WHERE OU.[OrganizationId] = @OrganizationId
    ),

    AllOrgUsers AS (
        SELECT
            OU.[Id] AS [OrganizationUserId],
            OU.[UserId],
            OU.[OrganizationId],
            AU.[Email],
            OU.[Type] AS [OrganizationUserType],
            OU.[Status] AS [OrganizationUserStatus],
            OU.[Permissions] AS [OrganizationUserPermissionsData]
        FROM [dbo].[OrganizationUserView] OU
            INNER JOIN GivenOrgUsers AU ON AU.[UserId] = OU.[UserId]
        UNION ALL
        SELECT
            OU.[Id] AS [OrganizationUserId],
            AU.[UserId],
            OU.[OrganizationId],
            AU.[Email],
            OU.[Type] AS [OrganizationUserType],
            OU.[Status] AS [OrganizationUserStatus],
            OU.[Permissions] AS [OrganizationUserPermissionsData]
        FROM [dbo].[OrganizationUserView] OU
            INNER JOIN GivenOrgUsers AU ON AU.[Email] = OU.[Email]
    )

    SELECT
        OU.[OrganizationUserId],
        P.[OrganizationId],
        P.[Type] AS [PolicyType],
            P.[Data] AS [PolicyData],
            OU.[OrganizationUserType],
            OU.[OrganizationUserStatus],
            OU.[OrganizationUserPermissionsData],
            CASE
                WHEN EXISTS (
                    SELECT 1
                    FROM [dbo].[ProviderUserView] PU
                    INNER JOIN [dbo].[ProviderOrganizationView] PO ON PO.[ProviderId] = PU.[ProviderId]
                    WHERE PU.[UserId] = OU.[UserId]
                    AND PO.[OrganizationId] = P.[OrganizationId]
                ) THEN 1
                ELSE 0
            END AS [IsProvider]
    FROM [dbo].[PolicyView] P
    INNER JOIN [dbo].[OrganizationView] O ON P.[OrganizationId] = O.[Id]
    INNER JOIN AllOrgUsers OU ON OU.[OrganizationId] = O.[Id]
    WHERE P.[Enabled] = 1
      AND O.[Enabled] = 1
      AND O.[UsePolicies] = 1
      AND P.[Type] = @PolicyType

END
GO
