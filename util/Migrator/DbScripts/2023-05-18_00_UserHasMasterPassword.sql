CREATE OR ALTER VIEW [dbo].[OrganizationUserUserDetailsView]
AS
SELECT
    OU.[Id],
    OU.[UserId],
    OU.[OrganizationId],
    U.[Name],
    ISNULL(U.[Email], OU.[Email]) Email,
    U.[AvatarColor],
    U.[TwoFactorProviders],
    U.[Premium],
    OU.[Status],
    OU.[Type],
    OU.[AccessAll],
    OU.[AccessSecretsManager],
    OU.[ExternalId],
    SU.[ExternalId] SsoExternalId,
    OU.[Permissions],
    OU.[ResetPasswordKey],
    U.[UsesKeyConnector],
    CASE WHEN U.[MasterPassword] IS NOT NULL THEN 1 ELSE 0 END AS HasMasterPassword
FROM
    [dbo].[OrganizationUser] OU
LEFT JOIN
    [dbo].[User] U ON U.[Id] = OU.[UserId]
LEFT JOIN
    [dbo].[SsoUser] SU ON SU.[UserId] = OU.[UserId] AND SU.[OrganizationId] = OU.[OrganizationId]
GO

IF OBJECT_ID('[dbo].[OrganizationUserUserDetails_ReadByOrganizationId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationUserUserDetails_ReadByOrganizationId]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationUser_ReadByMinimumRole]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationUser_ReadByMinimumRole]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationUserUserDetails_ReadById]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationUserUserDetails_ReadById]';
END
GO

CREATE OR ALTER VIEW [dbo].[ProviderUserUserDetailsView]
AS
SELECT
    PU.[Id],
    PU.[UserId],
    PU.[ProviderId],
    U.[Name],
    ISNULL(U.[Email], PU.[Email]) Email,
    PU.[Status],
    PU.[Type],
    PU.[Permissions],
    CASE WHEN U.[MasterPassword] IS NOT NULL THEN 1 ELSE 0 END AS HasMasterPassword
FROM
    [dbo].[ProviderUser] PU
LEFT JOIN
    [dbo].[User] U ON U.[Id] = PU.[UserId]
GO

IF OBJECT_ID('[dbo].[ProviderUserUserDetails_ReadByProviderId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[ProviderUserUserDetails_ReadByProviderId]';
END
GO

CREATE OR ALTER PROCEDURE [dbo].[ProviderUserUserDetails_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderUserUserDetailsView]
    WHERE
        [Id] = @Id
END
GO