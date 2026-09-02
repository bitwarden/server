CREATE OR ALTER VIEW [dbo].[OrganizationUserOrganizationDetailsView]
AS
SELECT
    OU.[UserId],
    OU.[OrganizationId],
    OU.[Id] OrganizationUserId,
    O.[Name],
    O.[Enabled],
    O.[PlanType],
    O.[UsePolicies],
    O.[UseSso],
    O.[UseKeyConnector],
    O.[UseScim],
    O.[UseGroups],
    O.[UseDirectory],
    O.[UseEvents],
    O.[UseTotp],
    O.[Use2fa],
    O.[UseApi],
    O.[UseResetPassword],
    O.[SelfHost],
    O.[UsersGetPremium],
    O.[UseCustomPermissions],
    O.[UseSecretsManager],
    O.[Seats],
    O.[MaxCollections],
    O.[MaxStorageGb],
    O.[Identifier],
    OU.[Key],
    OU.[ResetPasswordKey],
    O.[PublicKey],
    O.[PrivateKey],
    OU.[Status],
    OU.[Type],
    SU.[ExternalId] SsoExternalId,
    OU.[Permissions],
    PO.[ProviderId],
    P.[Name] ProviderName,
    P.[Type] ProviderType,
    SS.[Data] SsoConfig,
    OS.[FriendlyName] FamilySponsorshipFriendlyName,
    OS.[LastSyncDate] FamilySponsorshipLastSyncDate,
    OS.[ToDelete] FamilySponsorshipToDelete,
    OS.[ValidUntil] FamilySponsorshipValidUntil,
    OU.[AccessSecretsManager],
    O.[UsePasswordManager],
    O.[SmSeats],
    O.[SmServiceAccounts],
    O.[LimitCollectionCreation],
    O.[LimitCollectionDeletion],
    O.[AllowAdminAccessToAllCollectionItems],
    O.[UseRiskInsights],
    O.[UseAdminSponsoredFamilies],
    O.[LimitItemDeletion],
    OS.[IsAdminInitiated]
FROM
    [dbo].[OrganizationUser] OU
    LEFT JOIN
    [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId]
    LEFT JOIN
    [dbo].[SsoUser] SU ON SU.[UserId] = OU.[UserId] AND SU.[OrganizationId] = OU.[OrganizationId]
    LEFT JOIN
    [dbo].[ProviderOrganization] PO ON PO.[OrganizationId] = O.[Id]
    LEFT JOIN
    [dbo].[Provider] P ON P.[Id] = PO.[ProviderId]
    LEFT JOIN
    [dbo].[SsoConfig] SS ON SS.[OrganizationId] = OU.[OrganizationId]
    LEFT JOIN
    [dbo].[OrganizationSponsorship] OS ON OS.[SponsoringOrganizationUserID] = OU.[Id]
GO

--Manually refresh [dbo].[OrganizationUserOrganizationDetailsView]
IF OBJECT_ID('[dbo].[OrganizationUserOrganizationDetailsView]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationUserOrganizationDetailsView]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationView]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationView]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationSponsorshipView]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationSponsorshipView]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationUserOrganizationDetailsView]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationUserOrganizationDetailsView]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationSponsorship_OrganizationUserDeleted]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationSponsorship_OrganizationUserDeleted]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationSponsorship_CreateMany]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationSponsorship_CreateMany]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationSponsorship_OrganizationUsersDeleted]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationSponsorship_OrganizationUsersDeleted]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationSponsorship_DeleteExpired]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationSponsorship_DeleteExpired]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationSponsorship_Update]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationSponsorship_Update]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationSponsorship_DeleteById]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationSponsorship_DeleteById]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationSponsorship_Create]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationSponsorship_Create]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationSponsorship_OrganizationDeleted]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationSponsorship_OrganizationDeleted]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationSponsorship_UpdateMany]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationSponsorship_UpdateMany]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationSponsorship_DeleteByIds]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationSponsorship_DeleteByIds]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationSponsorship_ReadLatestBySponsoringOrganizationId]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationSponsorship_ReadLatestBySponsoringOrganizationId]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationSponsorship_ReadByOfferedToEmail]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationSponsorship_ReadByOfferedToEmail]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationSponsorship_ReadBySponsoredOrganizationId]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationSponsorship_ReadBySponsoredOrganizationId]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationSponsorship_ReadBySponsoringOrganizationId]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationSponsorship_ReadBySponsoringOrganizationId]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationSponsorship_ReadById]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationSponsorship_ReadById]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationSponsorship_ReadBySponsoringOrganizationUserId]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationSponsorship_ReadBySponsoringOrganizationUserId]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatus]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatus]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatusOrganizationId]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatusOrganizationId]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationUser_DeleteById]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationUser_DeleteById]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationUser_DeleteByIds]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationUser_DeleteByIds]';
END
GO

IF OBJECT_ID('[dbo].[Organization_DeleteById]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[Organization_DeleteById]';
END
GO
