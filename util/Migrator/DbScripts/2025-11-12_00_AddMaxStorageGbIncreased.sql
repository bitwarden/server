-- Add MaxStorageGbIncreased column to User table
IF COL_LENGTH('[dbo].[User]', 'MaxStorageGbIncreased') IS NULL
BEGIN
    ALTER TABLE [dbo].[User] ADD [MaxStorageGbIncreased] SMALLINT NULL;
END
GO

-- Add MaxStorageGbIncreased column to Organization table
IF COL_LENGTH('[dbo].[Organization]', 'MaxStorageGbIncreased') IS NULL
BEGIN
    ALTER TABLE [dbo].[Organization] ADD [MaxStorageGbIncreased] SMALLINT NULL;
END
GO

-- Update UserView to use COALESCE for MaxStorageGb
CREATE OR ALTER VIEW [dbo].[UserView]
AS
SELECT
    [Id],
    [Name],
    [Email],
    [EmailVerified],
    [MasterPassword],
    [MasterPasswordHint],
    [Culture],
    [SecurityStamp],
    [TwoFactorProviders],
    [TwoFactorRecoveryCode],
    [EquivalentDomains],
    [ExcludedGlobalEquivalentDomains],
    [AccountRevisionDate],
    [Key],
    [PublicKey],
    [PrivateKey],
    [Premium],
    [PremiumExpirationDate],
    [RenewalReminderDate],
    [Storage],
    COALESCE([MaxStorageGbIncreased], [MaxStorageGb]) AS [MaxStorageGb],
    [Gateway],
    [GatewayCustomerId],
    [GatewaySubscriptionId],
    [ReferenceData],
    [LicenseKey],
    [ApiKey],
    [Kdf],
    [KdfIterations],
    [KdfMemory],
    [KdfParallelism],
    [CreationDate],
    [RevisionDate],
    [ForcePasswordReset],
    [UsesKeyConnector],
    [FailedLoginCount],
    [LastFailedLoginDate],
    [AvatarColor],
    [LastPasswordChangeDate],
    [LastKdfChangeDate],
    [LastKeyRotationDate],
    [LastEmailChangeDate],
    [VerifyDevices],
    [SecurityState],
    [SecurityVersion],
    [SignedPublicKey]
FROM
    [dbo].[User]
GO

-- Update OrganizationView to use COALESCE for MaxStorageGb
CREATE OR ALTER VIEW [dbo].[OrganizationView]
AS
SELECT
    [Id],
    [Identifier],
    [Name],
    [BusinessName],
    [BusinessAddress1],
    [BusinessAddress2],
    [BusinessAddress3],
    [BusinessCountry],
    [BusinessTaxNumber],
    [BillingEmail],
    [Plan],
    [PlanType],
    [Seats],
    [MaxCollections],
    [UsePolicies],
    [UseSso],
    [UseGroups],
    [UseDirectory],
    [UseEvents],
    [UseTotp],
    [Use2fa],
    [UseApi],
    [UseResetPassword],
    [SelfHost],
    [UsersGetPremium],
    [Storage],
    COALESCE([MaxStorageGbIncreased], [MaxStorageGb]) AS [MaxStorageGb],
    [Gateway],
    [GatewayCustomerId],
    [GatewaySubscriptionId],
    [ReferenceData],
    [Enabled],
    [LicenseKey],
    [PublicKey],
    [PrivateKey],
    [TwoFactorProviders],
    [ExpirationDate],
    [CreationDate],
    [RevisionDate],
    [OwnersNotifiedOfAutoscaling],
    [MaxAutoscaleSeats],
    [UseKeyConnector],
    [UseScim],
    [UseCustomPermissions],
    [UseSecretsManager],
    [Status],
    [UsePasswordManager],
    [SmSeats],
    [SmServiceAccounts],
    [MaxAutoscaleSmSeats],
    [MaxAutoscaleSmServiceAccounts],
    [SecretsManagerBeta],
    [LimitCollectionCreation],
    [LimitCollectionDeletion],
    [LimitItemDeletion],
    [AllowAdminAccessToAllCollectionItems],
    [UseRiskInsights],
    [UseOrganizationDomains],
    [UseAdminSponsoredFamilies],
    [SyncSeats],
    [UseAutomaticUserConfirmation]
FROM
    [dbo].[Organization]
GO


-- Update OrganizationUserOrganizationDetailsView
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
    COALESCE(O.[MaxStorageGbIncreased], O.[MaxStorageGb]) AS [MaxStorageGb],
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
    SS.[Enabled] SsoEnabled,
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
    O.[LimitItemDeletion],
    O.[UseAdminSponsoredFamilies],
    O.[UseOrganizationDomains],
    OS.[IsAdminInitiated],
    O.[UseAutomaticUserConfirmation]
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

-- Update ProviderUserProviderOrganizationDetailsView
CREATE OR ALTER VIEW [dbo].[ProviderUserProviderOrganizationDetailsView]
AS
SELECT
    PU.[UserId],
    PO.[OrganizationId],
    O.[Name],
    O.[Enabled],
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
    O.[UseSecretsManager],
    O.[UsePasswordManager],
    O.[SelfHost],
    O.[UsersGetPremium],
    O.[UseCustomPermissions],
    O.[Seats],
    O.[MaxCollections],
    COALESCE(O.[MaxStorageGbIncreased], O.[MaxStorageGb]) AS [MaxStorageGb],
    O.[Identifier],
    PO.[Key],
    O.[PublicKey],
    O.[PrivateKey],
    PU.[Status],
    PU.[Type],
    PO.[ProviderId],
    PU.[Id] ProviderUserId,
    P.[Name] ProviderName,
    O.[PlanType],
    O.[LimitCollectionCreation],
    O.[LimitCollectionDeletion],
    O.[AllowAdminAccessToAllCollectionItems],
    O.[UseRiskInsights],
    O.[UseAdminSponsoredFamilies],
    P.[Type] ProviderType,
    O.[LimitItemDeletion],
    O.[UseOrganizationDomains],
    O.[UseAutomaticUserConfirmation],
    SS.[Enabled] SsoEnabled,
    SS.[Data] SsoConfig
FROM
    [dbo].[ProviderUser] PU
INNER JOIN
    [dbo].[ProviderOrganization] PO ON PO.[ProviderId] = PU.[ProviderId]
INNER JOIN
    [dbo].[Organization] O ON O.[Id] = PO.[OrganizationId]
INNER JOIN
    [dbo].[Provider] P ON P.[Id] = PU.[ProviderId]
LEFT JOIN
    [dbo].[SsoConfig] SS ON SS.[OrganizationId] = O.[Id]
GO

-- Refresh views that reference Organization table
EXEC sp_refreshview N'[dbo].[OrganizationCipherDetailsCollectionsView]';
EXEC sp_refreshview N'[dbo].[OrganizationUserOrganizationDetailsView]';
EXEC sp_refreshview N'[dbo].[ProviderOrganizationOrganizationDetailsView]';
EXEC sp_refreshview N'[dbo].[ProviderUserProviderOrganizationDetailsView]';
GO

-- Refresh views that reference User table
EXEC sp_refreshview N'[dbo].[EmergencyAccessDetailsView]';
EXEC sp_refreshview N'[dbo].[OrganizationUserUserDetailsView]';
EXEC sp_refreshview N'[dbo].[ProviderUserUserDetailsView]';
EXEC sp_refreshview N'[dbo].[UserEmailDomainView]';
GO

-- Refresh stored procedures that reference UserView
EXEC sp_refreshsqlmodule N'[dbo].[Notification_ReadByUserIdAndStatus]';
EXEC sp_refreshsqlmodule N'[dbo].[OrganizationUser_ReadById]';
EXEC sp_refreshsqlmodule N'[dbo].[OrganizationUser_ReadByIds]';
EXEC sp_refreshsqlmodule N'[dbo].[OrganizationUser_ReadByOrganizationId]';
EXEC sp_refreshsqlmodule N'[dbo].[OrganizationUser_ReadByOrganizationIdEmail]';
EXEC sp_refreshsqlmodule N'[dbo].[OrganizationUser_ReadByOrganizationIdUserId]';
EXEC sp_refreshsqlmodule N'[dbo].[OrganizationUser_ReadByOrganizationIdWithClaimedDomains]';
EXEC sp_refreshsqlmodule N'[dbo].[OrganizationUser_ReadByOrganizationIdWithClaimedDomains_V2]';
EXEC sp_refreshsqlmodule N'[dbo].[OrganizationUser_ReadByUserId]';
EXEC sp_refreshsqlmodule N'[dbo].[OrganizationUser_ReadByUserIds]';
EXEC sp_refreshsqlmodule N'[dbo].[OrganizationUser_ReadByUserIdWithPolicyDetails]';
EXEC sp_refreshsqlmodule N'[dbo].[OrganizationUser_ReadOccupiedSeatCountByOrganizationId]';
EXEC sp_refreshsqlmodule N'[dbo].[OrganizationUser_ReadOccupiedSmSeatCountByOrganizationId]';
EXEC sp_refreshsqlmodule N'[dbo].[ProviderUser_ReadById]';
EXEC sp_refreshsqlmodule N'[dbo].[ProviderUser_ReadByIds]';
EXEC sp_refreshsqlmodule N'[dbo].[ProviderUser_ReadByOrganizationIdStatus]';
EXEC sp_refreshsqlmodule N'[dbo].[ProviderUser_ReadByProviderId]';
EXEC sp_refreshsqlmodule N'[dbo].[ProviderUser_ReadByProviderIdUserId]';
EXEC sp_refreshsqlmodule N'[dbo].[ProviderUser_ReadByUserId]';
EXEC sp_refreshsqlmodule N'[dbo].[User_ReadByEmail]';
EXEC sp_refreshsqlmodule N'[dbo].[User_ReadByEmails]';
EXEC sp_refreshsqlmodule N'[dbo].[User_ReadById]';
EXEC sp_refreshsqlmodule N'[dbo].[User_ReadByIds]';
EXEC sp_refreshsqlmodule N'[dbo].[User_ReadByIdsWithCalculatedPremium]';
EXEC sp_refreshsqlmodule N'[dbo].[User_ReadByPremium]';
EXEC sp_refreshsqlmodule N'[dbo].[User_ReadBySsoUserOrganizationIdExternalId]';
EXEC sp_refreshsqlmodule N'[dbo].[User_Search]';
GO

-- Refresh stored procedures that reference OrganizationView
EXEC sp_refreshsqlmodule N'[dbo].[Organization_GetOrganizationsForSubscriptionSync]';
EXEC sp_refreshsqlmodule N'[dbo].[Organization_ReadByClaimedUserEmailDomain]';
EXEC sp_refreshsqlmodule N'[dbo].[Organization_ReadByEnabled]';
EXEC sp_refreshsqlmodule N'[dbo].[Organization_ReadById]';
EXEC sp_refreshsqlmodule N'[dbo].[Organization_ReadByIdentifier]';
EXEC sp_refreshsqlmodule N'[dbo].[Organization_ReadByLicenseKey]';
EXEC sp_refreshsqlmodule N'[dbo].[Organization_ReadByProviderId]';
EXEC sp_refreshsqlmodule N'[dbo].[Organization_ReadByUserId]';
EXEC sp_refreshsqlmodule N'[dbo].[Organization_ReadManyByIds]';
EXEC sp_refreshsqlmodule N'[dbo].[Organization_ReadOccupiedSeatCountByOrganizationId]';
EXEC sp_refreshsqlmodule N'[dbo].[Organization_Search]';
EXEC sp_refreshsqlmodule N'[dbo].[Organization_UnassignedToProviderSearch]';
EXEC sp_refreshsqlmodule N'[dbo].[OrganizationDomainSsoDetails_ReadByEmail]';
EXEC sp_refreshsqlmodule N'[dbo].[PolicyDetails_ReadByOrganizationId]';
EXEC sp_refreshsqlmodule N'[dbo].[PolicyDetails_ReadByUserId]';
EXEC sp_refreshsqlmodule N'[dbo].[PolicyDetails_ReadByUserIdsPolicyType]';
EXEC sp_refreshsqlmodule N'[dbo].[ProviderOrganization_ReadById]';
EXEC sp_refreshsqlmodule N'[dbo].[ProviderOrganization_ReadByOrganizationId]';
EXEC sp_refreshsqlmodule N'[dbo].[ProviderOrganization_ReadCountByOrganizationIds]';
EXEC sp_refreshsqlmodule N'[dbo].[ProviderOrganizationProviderDetails_ReadByUserId]';
EXEC sp_refreshsqlmodule N'[dbo].[VerifiedOrganizationDomainSsoDetails_ReadByEmail]';
GO

-- Refresh stored procedures that reference OrganizationUserOrganizationDetailsView
EXEC sp_refreshsqlmodule N'[dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatus]';
EXEC sp_refreshsqlmodule N'[dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatusOrganizationId]';
GO

-- Refresh stored procedures that reference ProviderUserProviderOrganizationDetailsView
EXEC sp_refreshsqlmodule N'[dbo].[ProviderUserProviderOrganizationDetails_ReadByUserIdStatus]';
GO

