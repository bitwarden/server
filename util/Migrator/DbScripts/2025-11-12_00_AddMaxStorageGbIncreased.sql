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
    [SignedPublicKey],
    [MaxStorageGbIncreased]
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
    [UseAutomaticUserConfirmation],
    [MaxStorageGbIncreased]
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
EXEC sp_refreshview N'[dbo].[ProviderOrganizationOrganizationDetailsView]';
GO

-- Refresh views that reference User table
EXEC sp_refreshview N'[dbo].[EmergencyAccessDetailsView]';
EXEC sp_refreshview N'[dbo].[OrganizationUserUserDetailsView]';
EXEC sp_refreshview N'[dbo].[ProviderUserUserDetailsView]';
EXEC sp_refreshview N'[dbo].[UserEmailDomainView]';
GO

