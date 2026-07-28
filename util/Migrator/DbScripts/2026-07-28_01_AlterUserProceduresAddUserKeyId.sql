CREATE OR ALTER PROCEDURE [dbo].[User_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @Name NVARCHAR(50),
    @Email NVARCHAR(256),
    @EmailVerified BIT,
    @MasterPassword NVARCHAR(300),
    @MasterPasswordHint NVARCHAR(50),
    @Culture NVARCHAR(10),
    @SecurityStamp NVARCHAR(50),
    @TwoFactorProviders NVARCHAR(MAX),
    @TwoFactorRecoveryCode NVARCHAR(32),
    @EquivalentDomains NVARCHAR(MAX),
    @ExcludedGlobalEquivalentDomains NVARCHAR(MAX),
    @AccountRevisionDate DATETIME2(7),
    @Key NVARCHAR(MAX),
    @PublicKey NVARCHAR(MAX),
    @PrivateKey NVARCHAR(MAX),
    @Premium BIT,
    @PremiumExpirationDate DATETIME2(7),
    @RenewalReminderDate DATETIME2(7),
    @Storage BIGINT,
    @MaxStorageGb SMALLINT,
    @Gateway TINYINT,
    @GatewayCustomerId VARCHAR(50),
    @GatewaySubscriptionId VARCHAR(50),
    @ReferenceData VARCHAR(MAX),
    @LicenseKey VARCHAR(100),
    @Kdf TINYINT,
    @KdfIterations INT,
    @KdfMemory INT = NULL,
    @KdfParallelism INT = NULL,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @ApiKey VARCHAR(30),
    @ForcePasswordReset BIT = 0,
    @UsesKeyConnector BIT = 0,
    @FailedLoginCount INT = 0,
    @LastFailedLoginDate DATETIME2(7),
    @AvatarColor VARCHAR(7) = NULL,
    @LastPasswordChangeDate DATETIME2(7) = NULL,
    @LastKdfChangeDate DATETIME2(7) = NULL,
    @LastKeyRotationDate DATETIME2(7) = NULL,
    @LastEmailChangeDate DATETIME2(7) = NULL,
    @VerifyDevices BIT = 1,
    @SecurityState VARCHAR(MAX) = NULL,
    @SecurityVersion INT = NULL,
    @SignedPublicKey VARCHAR(MAX) = NULL,
    @V2UpgradeToken VARCHAR(MAX) = NULL,
    @MasterPasswordSalt NVARCHAR(256) = NULL,
    @LastApiKeyRotationDate DATETIME2(7) = NULL,
    @UserKeyId VARCHAR(64) = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[User]
    (
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
        [MaxStorageGb],
        [Gateway],
        [GatewayCustomerId],
        [GatewaySubscriptionId],
        [ReferenceData],
        [LicenseKey],
        [Kdf],
        [KdfIterations],
        [CreationDate],
        [RevisionDate],
        [ApiKey],
        [ForcePasswordReset],
        [UsesKeyConnector],
        [FailedLoginCount],
        [LastFailedLoginDate],
        [AvatarColor],
        [KdfMemory],
        [KdfParallelism],
        [LastPasswordChangeDate],
        [LastKdfChangeDate],
        [LastKeyRotationDate],
        [LastEmailChangeDate],
        [VerifyDevices],
        [SecurityState],
        [SecurityVersion],
        [SignedPublicKey],
        [MaxStorageGbIncreased],
        [V2UpgradeToken],
        [MasterPasswordSalt],
        [LastApiKeyRotationDate],
        [UserKeyId]
    )
    VALUES
    (
        @Id,
        @Name,
        @Email,
        @EmailVerified,
        @MasterPassword,
        @MasterPasswordHint,
        @Culture,
        @SecurityStamp,
        @TwoFactorProviders,
        @TwoFactorRecoveryCode,
        @EquivalentDomains,
        @ExcludedGlobalEquivalentDomains,
        @AccountRevisionDate,
        @Key,
        @PublicKey,
        @PrivateKey,
        @Premium,
        @PremiumExpirationDate,
        @RenewalReminderDate,
        @Storage,
        @MaxStorageGb,
        @Gateway,
        @GatewayCustomerId,
        @GatewaySubscriptionId,
        @ReferenceData,
        @LicenseKey,
        @Kdf,
        @KdfIterations,
        @CreationDate,
        @RevisionDate,
        @ApiKey,
        @ForcePasswordReset,
        @UsesKeyConnector,
        @FailedLoginCount,
        @LastFailedLoginDate,
        @AvatarColor,
        @KdfMemory,
        @KdfParallelism,
        @LastPasswordChangeDate,
        @LastKdfChangeDate,
        @LastKeyRotationDate,
        @LastEmailChangeDate,
        @VerifyDevices,
        @SecurityState,
        @SecurityVersion,
        @SignedPublicKey,
        @MaxStorageGb,
        @V2UpgradeToken,
        @MasterPasswordSalt,
        @LastApiKeyRotationDate,
        @UserKeyId
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[User_Update]
    @Id UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @Email NVARCHAR(256),
    @EmailVerified BIT,
    @MasterPassword NVARCHAR(300),
    @MasterPasswordHint NVARCHAR(50),
    @Culture NVARCHAR(10),
    @SecurityStamp NVARCHAR(50),
    @TwoFactorProviders NVARCHAR(MAX),
    @TwoFactorRecoveryCode NVARCHAR(32),
    @EquivalentDomains NVARCHAR(MAX),
    @ExcludedGlobalEquivalentDomains NVARCHAR(MAX),
    @AccountRevisionDate DATETIME2(7),
    @Key NVARCHAR(MAX),
    @PublicKey NVARCHAR(MAX),
    @PrivateKey NVARCHAR(MAX),
    @Premium BIT,
    @PremiumExpirationDate DATETIME2(7),
    @RenewalReminderDate DATETIME2(7),
    @Storage BIGINT,
    @MaxStorageGb SMALLINT,
    @Gateway TINYINT,
    @GatewayCustomerId VARCHAR(50),
    @GatewaySubscriptionId VARCHAR(50),
    @ReferenceData VARCHAR(MAX),
    @LicenseKey VARCHAR(100),
    @Kdf TINYINT,
    @KdfIterations INT,
    @KdfMemory INT = NULL,
    @KdfParallelism INT = NULL,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @ApiKey VARCHAR(30),
    @ForcePasswordReset BIT = 0,
    @UsesKeyConnector BIT = 0,
    @FailedLoginCount INT,
    @LastFailedLoginDate DATETIME2(7),
    @AvatarColor VARCHAR(7),
    @LastPasswordChangeDate DATETIME2(7) = NULL,
    @LastKdfChangeDate DATETIME2(7) = NULL,
    @LastKeyRotationDate DATETIME2(7) = NULL,
    @LastEmailChangeDate DATETIME2(7) = NULL,
    @VerifyDevices BIT = 1,
    @SecurityState VARCHAR(MAX) = NULL,
    @SecurityVersion INT = NULL,
    @SignedPublicKey VARCHAR(MAX) = NULL,
    @V2UpgradeToken VARCHAR(MAX) = NULL,
    @MasterPasswordSalt NVARCHAR(256) = NULL,
    @LastApiKeyRotationDate DATETIME2(7) = NULL,
    @UserKeyId VARCHAR(64) = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[User]
    SET
        [Name] = @Name,
        [Email] = @Email,
        [EmailVerified] = @EmailVerified,
        [MasterPassword] = @MasterPassword,
        [MasterPasswordHint] = @MasterPasswordHint,
        [Culture] = @Culture,
        [SecurityStamp] = @SecurityStamp,
        [TwoFactorProviders] = @TwoFactorProviders,
        [TwoFactorRecoveryCode] = @TwoFactorRecoveryCode,
        [EquivalentDomains] = @EquivalentDomains,
        [ExcludedGlobalEquivalentDomains] = @ExcludedGlobalEquivalentDomains,
        [AccountRevisionDate] = @AccountRevisionDate,
        [Key] = @Key,
        [PublicKey] = @PublicKey,
        [PrivateKey] = @PrivateKey,
        [Premium] = @Premium,
        [PremiumExpirationDate] = @PremiumExpirationDate,
        [RenewalReminderDate] = @RenewalReminderDate,
        [Storage] = @Storage,
        [MaxStorageGb] = @MaxStorageGb,
        [Gateway] = @Gateway,
        [GatewayCustomerId] = @GatewayCustomerId,
        [GatewaySubscriptionId] = @GatewaySubscriptionId,
        [ReferenceData] = @ReferenceData,
        [LicenseKey] = @LicenseKey,
        [Kdf] = @Kdf,
        [KdfIterations] = @KdfIterations,
        [KdfMemory] = @KdfMemory,
        [KdfParallelism] = @KdfParallelism,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [ApiKey] = @ApiKey,
        [ForcePasswordReset] = @ForcePasswordReset,
        [UsesKeyConnector] = @UsesKeyConnector,
        [FailedLoginCount] = @FailedLoginCount,
        [LastFailedLoginDate] = @LastFailedLoginDate,
        [AvatarColor] = @AvatarColor,
        [LastPasswordChangeDate] = @LastPasswordChangeDate,
        [LastKdfChangeDate] = @LastKdfChangeDate,
        [LastKeyRotationDate] = @LastKeyRotationDate,
        [LastEmailChangeDate] = @LastEmailChangeDate,
        [VerifyDevices] = @VerifyDevices,
        [SecurityState] = @SecurityState,
        [SecurityVersion] = @SecurityVersion,
        [SignedPublicKey] = @SignedPublicKey,
        [MaxStorageGbIncreased] = @MaxStorageGb,
        [V2UpgradeToken] = @V2UpgradeToken,
        [MasterPasswordSalt] = @MasterPasswordSalt,
        [LastApiKeyRotationDate] = @LastApiKeyRotationDate,
        [UserKeyId] = @UserKeyId
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[User_UpdateKeys]
    @Id UNIQUEIDENTIFIER,
    @SecurityStamp NVARCHAR(50),
    @Key NVARCHAR(MAX),
    @PrivateKey VARCHAR(MAX),
    @RevisionDate DATETIME2(7),
    @AccountRevisionDate DATETIME2(7) = NULL,
    @LastKeyRotationDate DATETIME2(7) = NULL,
    @UserKeyId VARCHAR(64) = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[User]
    SET
        [SecurityStamp] = @SecurityStamp,
        [Key] = @Key,
        [PrivateKey] = @PrivateKey,
        [RevisionDate] = @RevisionDate,
        [AccountRevisionDate] = ISNULL(@AccountRevisionDate, @RevisionDate),
        [LastKeyRotationDate] = @LastKeyRotationDate,
        [UserKeyId] = @UserKeyId
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[User_UpdateMasterPassword]
    @Id UNIQUEIDENTIFIER,
    @MasterPassword NVARCHAR(300),
    @MasterPasswordHint NVARCHAR(50) = NULL,
    @Key VARCHAR(MAX),
    @Kdf TINYINT,
    @KdfIterations INT,
    @KdfMemory INT = NULL,
    @KdfParallelism INT = NULL,
    @RevisionDate DATETIME2(7),
    @AccountRevisionDate DATETIME2(7),
    @MasterPasswordSalt NVARCHAR(256) = NULL,
    @UserKeyId VARCHAR(64) = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[User]
    SET
        [MasterPassword] = @MasterPassword,
        [MasterPasswordHint] = @MasterPasswordHint,
        [Key] = @Key,
        [Kdf] = @Kdf,
        [KdfIterations] = @KdfIterations,
        [KdfMemory] = @KdfMemory,
        [KdfParallelism] = @KdfParallelism,
        [RevisionDate] = @RevisionDate,
        [AccountRevisionDate] = @AccountRevisionDate,
        [MasterPasswordSalt] = @MasterPasswordSalt,
        [UserKeyId] = @UserKeyId
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[User_UpdateMasterPasswordUnlockData]
    @Id UNIQUEIDENTIFIER,
    @Kdf TINYINT,
    @KdfIterations INT,
    @KdfMemory INT,
    @KdfParallelism INT,
    @MasterPasswordSalt NVARCHAR(256) = NULL,
    @Key VARCHAR(MAX),
    @RevisionDate DATETIME2(7),
    @AccountRevisionDate DATETIME2(7),
    @UserKeyId VARCHAR(64) = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[User]
    SET
        [Key] = @Key,
        [Kdf] = @Kdf,
        [KdfIterations] = @KdfIterations,
        [KdfMemory] = @KdfMemory,
        [KdfParallelism] = @KdfParallelism,
        [MasterPasswordSalt] = @MasterPasswordSalt,
        [RevisionDate] = @RevisionDate,
        [AccountRevisionDate] = @AccountRevisionDate,
        [UserKeyId] = @UserKeyId
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[User_TrySetUserKeyId]
    @Id UNIQUEIDENTIFIER,
    @UserKeyId VARCHAR(64),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The [UserKeyId] IS NULL predicate makes "only set when not already set" atomic,
    -- so two clients racing to backfill cannot both succeed.
    UPDATE
        [dbo].[User]
    SET
        [UserKeyId] = @UserKeyId,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
        AND [UserKeyId] IS NULL

    SELECT @@ROWCOUNT
END
GO
