IF OBJECT_ID('[dbo].[UserSignatureKeyPair]') IS NULL
BEGIN
    CREATE TABLE [dbo].[UserSignatureKeyPair]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [UserId] UNIQUEIDENTIFIER NOT NULL,
        [SignatureAlgorithm] TINYINT NOT NULL,
        [SigningKey] VARCHAR(MAX) NOT NULL,
        [VerifyingKey] VARCHAR(MAX) NOT NULL,
        [CreationDate] DATETIME2 (7) NOT NULL,
        [RevisionDate] DATETIME2 (7) NOT NULL,
        CONSTRAINT [PK_UserSignatureKeyPair] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_UserSignatureKeyPair_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
    );
END
GO

IF NOT EXISTS(SELECT name
FROM sys.indexes
WHERE name = 'IX_UserSignatureKeyPair_UserId')
BEGIN
CREATE UNIQUE NONCLUSTERED INDEX [IX_UserSignatureKeyPair_UserId]
    ON [dbo].[UserSignatureKeyPair]([UserId] ASC);
END
GO


CREATE OR ALTER VIEW [dbo].[UserSignatureKeyPairView]
AS
SELECT
    *
FROM
    [dbo].[UserSignatureKeyPair]
GO

CREATE OR ALTER PROCEDURE [dbo].[UserSignatureKeyPair_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM [dbo].[UserSignatureKeyPairView]
    WHERE [UserId] = @UserId;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[UserSignatureKeyPair_UpdateForRotation]
    @UserId UNIQUEIDENTIFIER,
    @SignatureAlgorithm TINYINT,
    @SigningKey VARCHAR(MAX),
    @VerifyingKey VARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[UserSignatureKeyPair]
    SET [SignatureAlgorithm] = @SignatureAlgorithm,
        [SigningKey] = @SigningKey,
        [VerifyingKey] = @VerifyingKey,
        [RevisionDate] = @RevisionDate
    WHERE [UserId] = @UserId;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[UserSignatureKeyPair_SetForRotation]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @SignatureAlgorithm TINYINT,
    @SigningKey VARCHAR(MAX),
    @VerifyingKey VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[UserSignatureKeyPair] ([Id], [UserId], [SignatureAlgorithm], [SigningKey], [VerifyingKey], [CreationDate], [RevisionDate])
    VALUES (@Id, @UserId, @SignatureAlgorithm, @SigningKey, @VerifyingKey, @CreationDate, @RevisionDate)
END
GO

IF COL_LENGTH('[dbo].[User]', 'SignedPublicKey') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[User]
    ADD
        [SignedPublicKey] VARCHAR(MAX) NULL;
END
GO

IF COL_LENGTH('[dbo].[User]', 'SecurityVersion') IS NULL
BEGIN
    ALTER TABLE [dbo].[User]
    ADD [SecurityVersion] INT NULL;
END
GO

IF COL_LENGTH('[dbo].[User]', 'SecurityState') IS NULL
BEGIN
    ALTER TABLE [dbo].[User]
    ADD [SecurityState] NVARCHAR(MAX) NULL;
END
GO

EXECUTE sp_refreshview 'dbo.UserView'
GO

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
    @SignedPublicKey VARCHAR(MAX) = NULL,
    @SecurityState VARCHAR(MAX) = NULL,
    @SecurityVersion INT = NULL
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
        [SignedPublicKey],
        [SecurityState],
        [SecurityVersion]
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
        @SignedPublicKey,
        @SecurityState,
        @SecurityVersion
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
    @SignedPublicKey VARCHAR(MAX) = NULL,
    @SecurityState VARCHAR(MAX) = NULL,
    @SecurityVersion INT = NULL
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
        [SignedPublicKey] = @SignedPublicKey,
        [SecurityState] = @SecurityState,
        [SecurityVersion] = @SecurityVersion
    WHERE
        [Id] = @Id
END
GO
