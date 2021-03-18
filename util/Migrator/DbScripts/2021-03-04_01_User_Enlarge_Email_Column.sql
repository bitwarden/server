-- Check email column is 50 characters long (100 bytes)
IF COL_LENGTH('[dbo].[User]', 'Email') = 100
BEGIN
	ALTER TABLE [dbo].[User] 
	ALTER COLUMN 
		Email NVARCHAR(256) NOT NULL
END
GO

-- Recreate procedure User_Create
IF OBJECT_ID('[dbo].[User_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_Create]
END
GO

CREATE PROCEDURE [dbo].[User_Create]
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
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @ApiKey VARCHAR(30)
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
        [ApiKey]
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
        @ApiKey
    )
END
GO

-- Recreate procedure User_Update
IF OBJECT_ID('[dbo].[User_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_Update]
END
GO

CREATE PROCEDURE [dbo].[User_Update]
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
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @ApiKey VARCHAR(30)
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
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [ApiKey] = @ApiKey
    WHERE
        [Id] = @Id
END
GO

-- Recreate procedure User_Search
IF OBJECT_ID('[dbo].[User_Search]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_Search]
END
GO

CREATE PROCEDURE [dbo].[User_Search]
    @Email NVARCHAR(256),
    @Skip INT = 0,
    @Take INT = 25
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @EmailLikeSearch NVARCHAR(261) = @Email + '%'

    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        (@Email IS NULL OR [Email] LIKE @EmailLikeSearch)
    ORDER BY [Email] ASC
    OFFSET @Skip ROWS
    FETCH NEXT @Take ROWS ONLY
END
GO

-- Recreate procedure User_ReadByEmail
IF OBJECT_ID('[dbo].[User_ReadByEmail]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_ReadByEmail]
END
GO

CREATE PROCEDURE [dbo].[User_ReadByEmail]
    @Email NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        [Email] = @Email
END
GO

-- Recreate procedure User_ReadKdfByEmail
IF OBJECT_ID('[dbo].[User_ReadKdfByEmail]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_ReadKdfByEmail]
END
GO

CREATE PROCEDURE [dbo].[User_ReadKdfByEmail]
    @Email NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Kdf],
        [KdfIterations]
    FROM
        [dbo].[User]
    WHERE
        [Email] = @Email
END
GO