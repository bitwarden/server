IF COL_LENGTH('[dbo].[User]', 'MasterPasswordSalt') IS NULL
BEGIN
    ALTER TABLE [dbo].[User] ADD [MasterPasswordSalt] NVARCHAR(256) NULL;
END
GO

-- Update UserView to include MasterPasswordSalt
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
        [V2UpgradeToken],
        [MasterPasswordSalt]
    FROM
        [dbo].[User]
GO

-- Refresh views that depend on UserView to ensure they include the new MasterPasswordSalt column
EXEC sp_refreshview N'[dbo].[EmergencyAccessDetailsView]';
EXEC sp_refreshview N'[dbo].[OrganizationUserUserDetailsView]';
EXEC sp_refreshview N'[dbo].[ProviderUserUserDetailsView]';
EXEC sp_refreshview N'[dbo].[UserEmailDomainView]';
EXEC sp_refreshview N'[dbo].[UserPremiumAccessView]';
GO
