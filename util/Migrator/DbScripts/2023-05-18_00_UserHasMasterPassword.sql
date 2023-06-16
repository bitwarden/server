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
