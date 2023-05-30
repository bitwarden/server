--Add column 'UsePasswordManager' to 'Organization' table
IF COL_LENGTH('[dbo].[Organization]', 'UsePasswordManager') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[Organization]
    ADD
        [UsePasswordManager] BIT NOT NULL CONSTRAINT [DF_Organization_UsePasswordManager] DEFAULT (1)
END
GO

--Add column 'SmSeats' to 'Organization' table
IF COL_LENGTH('[dbo].[Organization]', 'SmSeats') IS NULL
BEGIN
    ALTER TABLE [dbo].[Organization] ADD [SmSeats] INT NULL;
END
GO

--Add column 'SmServiceAccounts' to 'Organization' table
IF COL_LENGTH('[dbo].[Organization]', 'SmServiceAccounts') IS NULL
BEGIN
    ALTER TABLE [dbo].[Organization] ADD [SmServiceAccounts] INT NULL;
END
GO

--Add column 'MaxAutoscaleSmSeats' to 'Organization' table
IF COL_LENGTH('[dbo].[Organization]', 'MaxAutoscaleSmSeats') IS NULL
BEGIN
    ALTER TABLE [dbo].[Organization] ADD [MaxAutoscaleSmSeats] INT NULL;
END
GO

--Add column 'MaxAutoscaleSmServiceAccounts' to 'Organization' table
IF COL_LENGTH('[dbo].[Organization]', 'MaxAutoscaleSmServiceAccounts') IS NULL
BEGIN
    ALTER TABLE [dbo].[Organization] ADD [MaxAutoscaleSmServiceAccounts] INT NULL;
END
GO

--Insert value in column 'UsePasswordManager','SmSeats','SmServiceAccounts','MaxAutoscaleSmSeats','MaxAutoscaleSmServiceAccounts'
CREATE OR ALTER PROCEDURE [dbo].[Organization_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @Identifier NVARCHAR(50),
    @Name NVARCHAR(50),
    @BusinessName NVARCHAR(50),
    @BusinessAddress1 NVARCHAR(50),
    @BusinessAddress2 NVARCHAR(50),
    @BusinessAddress3 NVARCHAR(50),
    @BusinessCountry VARCHAR(2),
    @BusinessTaxNumber NVARCHAR(30),
    @BillingEmail NVARCHAR(256),
    @Plan NVARCHAR(50),
    @PlanType TINYINT,
    @Seats INT,
    @MaxCollections SMALLINT,
    @UsePolicies BIT,
    @UseSso BIT,
    @UseGroups BIT,
    @UseDirectory BIT,
    @UseEvents BIT,
    @UseTotp BIT,
    @Use2fa BIT,
    @UseApi BIT,
    @UseResetPassword BIT,
    @SelfHost BIT,
    @UsersGetPremium BIT,
    @Storage BIGINT,
    @MaxStorageGb SMALLINT,
    @Gateway TINYINT,
    @GatewayCustomerId VARCHAR(50),
    @GatewaySubscriptionId VARCHAR(50),
    @ReferenceData VARCHAR(MAX),
    @Enabled BIT,
    @LicenseKey VARCHAR(100),
    @PublicKey VARCHAR(MAX),
    @PrivateKey VARCHAR(MAX),
    @TwoFactorProviders NVARCHAR(MAX),
    @ExpirationDate DATETIME2(7),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @OwnersNotifiedOfAutoscaling DATETIME2(7),
    @MaxAutoscaleSeats INT,
    @UseKeyConnector BIT = 0,
    @UseScim BIT = 0,
    @UseCustomPermissions BIT = 0,
    @UseSecretsManager BIT = 0,
    @Status TINYINT = 0,
    @UsePasswordManager BIT = 1,
    @SmSeats INT,
    @SmServiceAccounts INT,
    @MaxAutoscaleSmSeats INT,
    @MaxAutoscaleSmServiceAccounts INT,
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Organization]
    (
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
        [MaxStorageGb],
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
    )
    VALUES
    (
        @Id,
        @Identifier,
        @Name,
        @BusinessName,
        @BusinessAddress1,
        @BusinessAddress2,
        @BusinessAddress3,
        @BusinessCountry,
        @BusinessTaxNumber,
        @BillingEmail,
        @Plan,
        @PlanType,
        @Seats,
        @MaxCollections,
        @UsePolicies,
        @UseSso,
        @UseGroups,
        @UseDirectory,
        @UseEvents,
        @UseTotp,
        @Use2fa,
        @UseApi,
        @UseResetPassword,
        @SelfHost,
        @UsersGetPremium,
        @Storage,
        @MaxStorageGb,
        @Gateway,
        @GatewayCustomerId,
        @GatewaySubscriptionId,
        @ReferenceData,
        @Enabled,
        @LicenseKey,
        @PublicKey,
        @PrivateKey,
        @TwoFactorProviders,
        @ExpirationDate,
        @CreationDate,
        @RevisionDate,
        @OwnersNotifiedOfAutoscaling,
        @MaxAutoscaleSeats,
        @UseKeyConnector,
        @UseScim,
        @UseCustomPermissions,
        @UseSecretsManager,
        @Status,
        @UsePasswordManager,
        @SmSeats,
        @SmServiceAccounts,
        @MaxAutoscaleSmSeats,
        @MaxAutoscaleSmServiceAccounts,
    )
END
GO

--Update column 'UsePasswordManager','SmSeats','SmServiceAccounts','MaxAutoscaleSmSeats','MaxAutoscaleSmServiceAccounts'
CREATE OR ALTER PROCEDURE [dbo].[Organization_Update]
    @Id UNIQUEIDENTIFIER,
    @Identifier NVARCHAR(50),
    @Name NVARCHAR(50),
    @BusinessName NVARCHAR(50),
    @BusinessAddress1 NVARCHAR(50),
    @BusinessAddress2 NVARCHAR(50),
    @BusinessAddress3 NVARCHAR(50),
    @BusinessCountry VARCHAR(2),
    @BusinessTaxNumber NVARCHAR(30),
    @BillingEmail NVARCHAR(256),
    @Plan NVARCHAR(50),
    @PlanType TINYINT,
    @Seats INT,
    @MaxCollections SMALLINT,
    @UsePolicies BIT,
    @UseSso BIT,
    @UseGroups BIT,
    @UseDirectory BIT,
    @UseEvents BIT,
    @UseTotp BIT,
    @Use2fa BIT,
    @UseApi BIT,
    @UseResetPassword BIT,
    @SelfHost BIT,
    @UsersGetPremium BIT,
    @Storage BIGINT,
    @MaxStorageGb SMALLINT,
    @Gateway TINYINT,
    @GatewayCustomerId VARCHAR(50),
    @GatewaySubscriptionId VARCHAR(50),
    @ReferenceData VARCHAR(MAX),
    @Enabled BIT,
    @LicenseKey VARCHAR(100),
    @PublicKey VARCHAR(MAX),
    @PrivateKey VARCHAR(MAX),
    @TwoFactorProviders NVARCHAR(MAX),
    @ExpirationDate DATETIME2(7),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @OwnersNotifiedOfAutoscaling DATETIME2(7),
    @MaxAutoscaleSeats INT,
    @UseKeyConnector BIT = 0,
    @UseScim BIT = 0,
    @UseCustomPermissions BIT = 0,
    @UseSecretsManager BIT = 0,
    @Status TINYINT = 0,
    @UsePasswordManager BIT = 1,
    @SmSeats INT,
    @SmServiceAccounts INT,
    @MaxAutoscaleSmSeats INT,
    @MaxAutoscaleSmServiceAccounts INT,
AS
BEGIN
    SET NOCOUNT ON

UPDATE
    [dbo].[Organization]
SET
    [Identifier] = @Identifier,
    [Name] = @Name,
    [BusinessName] = @BusinessName,
    [BusinessAddress1] = @BusinessAddress1,
    [BusinessAddress2] = @BusinessAddress2,
    [BusinessAddress3] = @BusinessAddress3,
    [BusinessCountry] = @BusinessCountry,
    [BusinessTaxNumber] = @BusinessTaxNumber,
    [BillingEmail] = @BillingEmail,
    [Plan] = @Plan,
    [PlanType] = @PlanType,
    [Seats] = @Seats,
    [MaxCollections] = @MaxCollections,
    [UsePolicies] = @UsePolicies,
    [UseSso] = @UseSso,
    [UseGroups] = @UseGroups,
    [UseDirectory] = @UseDirectory,
    [UseEvents] = @UseEvents,
    [UseTotp] = @UseTotp,
    [Use2fa] = @Use2fa,
    [UseApi] = @UseApi,
    [UseResetPassword] = @UseResetPassword,
    [SelfHost] = @SelfHost,
    [UsersGetPremium] = @UsersGetPremium,
    [Storage] = @Storage,
    [MaxStorageGb] = @MaxStorageGb,
    [Gateway] = @Gateway,
    [GatewayCustomerId] = @GatewayCustomerId,
    [GatewaySubscriptionId] = @GatewaySubscriptionId,
    [ReferenceData] = @ReferenceData,
    [Enabled] = @Enabled,
    [LicenseKey] = @LicenseKey,
    [PublicKey] = @PublicKey,
    [PrivateKey] = @PrivateKey,
    [TwoFactorProviders] = @TwoFactorProviders,
    [ExpirationDate] = @ExpirationDate,
    [CreationDate] = @CreationDate,
    [RevisionDate] = @RevisionDate,
    [OwnersNotifiedOfAutoscaling] = @OwnersNotifiedOfAutoscaling,
    [MaxAutoscaleSeats] = @MaxAutoscaleSeats,
    [UseKeyConnector] = @UseKeyConnector,
    [UseScim] = @UseScim,
    [UseCustomPermissions] = @UseCustomPermissions,
    [UseSecretsManager] = @UseSecretsManager,
    [Status] = @Status,
    [UsePasswordManager] = @UsePasswordManager,
    [SmSeats] = @SmSeats,
    [SmServiceAccounts] = @SmServiceAccounts,
    [MaxAutoscaleSmSeats] = @MaxAutoscaleSmSeats,
    [MaxAutoscaleSmServiceAccounts] = @MaxAutoscaleSmServiceAccounts
WHERE
    [Id] = @Id
END
GO

--Add column 'UsePasswordManager','SmSeats','SmServiceAccounts'
CREATE OR ALTER VIEW [dbo].[OrganizationUserOrganizationDetailsView]
AS
SELECT
    OU.[UserId],
    OU.[OrganizationId],
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
    O.[UsePasswordManager],
    O.[SmSeats],
    O.[SmServiceAccounts],
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
    OU.[AccessSecretsManager]
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