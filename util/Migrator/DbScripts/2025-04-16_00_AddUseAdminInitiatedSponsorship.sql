IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Organization]') AND name = 'UseAdminSponsoredFamilies')
BEGIN
    -- First drop the default constraint
    DECLARE @ConstraintName nvarchar(200)
    SELECT @ConstraintName = name FROM sys.default_constraints 
    WHERE parent_object_id = OBJECT_ID(N'[dbo].[Organization]')
    AND parent_column_id = (SELECT column_id FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Organization]') AND name = 'UseAdminSponsoredFamilies')
    
    IF @ConstraintName IS NOT NULL
        EXEC('ALTER TABLE [dbo].[Organization] DROP CONSTRAINT ' + @ConstraintName)
        
    -- Then drop the column
    ALTER TABLE [dbo].[Organization] DROP COLUMN [UseAdminSponsoredFamilies]
END
GO;

ALTER TABLE [dbo].[Organization] ADD [UseAdminSponsoredFamilies] bit NOT NULL CONSTRAINT [DF_Organization_UseAdminSponsoredFamilies] default (0)
GO;

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrganizationSponsorship]') AND name = 'IsAdminInitiated')
BEGIN
    -- First drop the default constraint
    DECLARE @ConstraintName nvarchar(200)
    SELECT @ConstraintName = name FROM sys.default_constraints 
    WHERE parent_object_id = OBJECT_ID(N'[dbo].[OrganizationSponsorship]')
    AND parent_column_id = (SELECT column_id FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrganizationSponsorship]') AND name = 'IsAdminInitiated')
    
    IF @ConstraintName IS NOT NULL
        EXEC('ALTER TABLE [dbo].[OrganizationSponsorship] DROP CONSTRAINT ' + @ConstraintName)
        
    -- Then drop the column
    ALTER TABLE [dbo].[OrganizationSponsorship] DROP COLUMN [IsAdminInitiated]
END
GO;

ALTER TABLE [dbo].[OrganizationSponsorship] ADD [IsAdminInitiated] BIT CONSTRAINT [DF_OrganizationSponsorship_IsAdminInitiated] DEFAULT (0) NOT NULL
GO;

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrganizationSponsorship]') AND name = 'Notes')
BEGIN
    -- Notes column doesn't have a default constraint, so we can just drop it
    ALTER TABLE [dbo].[OrganizationSponsorship] DROP COLUMN [Notes]
END
GO;

ALTER TABLE [dbo].[OrganizationSponsorship] ADD [Notes] NVARCHAR(512) NULL
GO;

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
    @SmSeats INT = null,
    @SmServiceAccounts INT = null,
    @MaxAutoscaleSmSeats INT= null,
    @MaxAutoscaleSmServiceAccounts INT = null,
    @SecretsManagerBeta BIT = 0,
    @LimitCollectionCreation BIT = NULL,
    @LimitCollectionDeletion BIT = NULL,
    @AllowAdminAccessToAllCollectionItems BIT = 0,
    @UseRiskInsights BIT = 0,
    @LimitItemDeletion BIT = 0,
    @UseAdminSponsoredFamilies BIT = 0
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
        [SecretsManagerBeta],
        [LimitCollectionCreation],
        [LimitCollectionDeletion],
        [AllowAdminAccessToAllCollectionItems],
        [UseRiskInsights],
        [LimitItemDeletion],
        [UseAdminSponsoredFamilies]
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
        @SecretsManagerBeta,
        @LimitCollectionCreation,
        @LimitCollectionDeletion,
        @AllowAdminAccessToAllCollectionItems,
        @UseRiskInsights,
        @LimitItemDeletion,
        @UseAdminSponsoredFamilies
    )
END
GO;

CREATE OR ALTER PROCEDURE [dbo].[Organization_ReadAbilities]
AS
BEGIN
    SET NOCOUNT ON

SELECT
    [Id],
    [UseEvents],
    [Use2fa],
    CASE
    WHEN [Use2fa] = 1 AND [TwoFactorProviders] IS NOT NULL AND [TwoFactorProviders] != '{}' THEN
    1
    ELSE
    0
END AS [Using2fa],
        [UsersGetPremium],
        [UseCustomPermissions],
        [UseSso],
        [UseKeyConnector],
        [UseScim],
        [UseResetPassword],
        [UsePolicies],
        [Enabled],
        [LimitCollectionCreation],
        [LimitCollectionDeletion],
        [AllowAdminAccessToAllCollectionItems],
        [UseRiskInsights],
        [LimitItemDeletion],
        [UseAdminSponsoredFamilies]
    FROM
        [dbo].[Organization]
END
GO;

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
    @SmSeats INT = null,
    @SmServiceAccounts INT = null,
    @MaxAutoscaleSmSeats INT = null,
    @MaxAutoscaleSmServiceAccounts INT = null,
    @SecretsManagerBeta BIT = 0,
    @LimitCollectionCreation BIT = null,
    @LimitCollectionDeletion BIT = null,
    @AllowAdminAccessToAllCollectionItems BIT = 0,
    @UseRiskInsights BIT = 0,
    @LimitItemDeletion BIT = 0,
    @UseAdminSponsoredFamilies BIT = 0
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
    [MaxAutoscaleSmServiceAccounts] = @MaxAutoscaleSmServiceAccounts,
    [SecretsManagerBeta] = @SecretsManagerBeta,
    [LimitCollectionCreation] = @LimitCollectionCreation,
    [LimitCollectionDeletion] = @LimitCollectionDeletion,
    [AllowAdminAccessToAllCollectionItems] = @AllowAdminAccessToAllCollectionItems,
    [UseRiskInsights] = @UseRiskInsights,
    [LimitItemDeletion] = @LimitItemDeletion,
    [UseAdminSponsoredFamilies] = @UseAdminSponsoredFamilies
WHERE
    [Id] = @Id
END
GO;

CREATE OR ALTER PROCEDURE [dbo].[OrganizationSponsorship_Update]
    @Id UNIQUEIDENTIFIER,
    @SponsoringOrganizationId UNIQUEIDENTIFIER,
    @SponsoringOrganizationUserID UNIQUEIDENTIFIER,
    @SponsoredOrganizationId UNIQUEIDENTIFIER,
    @FriendlyName NVARCHAR(256),
    @OfferedToEmail NVARCHAR(256),
    @PlanSponsorshipType TINYINT,
    @ToDelete BIT,
    @LastSyncDate DATETIME2 (7),
    @ValidUntil DATETIME2 (7),
    @IsAdminInitiated BIT = 0,
    @Notes NVARCHAR(512) = NULL
AS
BEGIN
    SET NOCOUNT ON

UPDATE
    [dbo].[OrganizationSponsorship]
SET
    [SponsoringOrganizationId] = @SponsoringOrganizationId,
    [SponsoringOrganizationUserID] = @SponsoringOrganizationUserID,
    [SponsoredOrganizationId] = @SponsoredOrganizationId,
    [FriendlyName] = @FriendlyName,
    [OfferedToEmail] = @OfferedToEmail,
    [PlanSponsorshipType] = @PlanSponsorshipType,
    [ToDelete] = @ToDelete,
    [LastSyncDate] = @LastSyncDate,
    [ValidUntil] = @ValidUntil,
    [IsAdminInitiated] = @IsAdminInitiated,
    [Notes] = @Notes
WHERE
    [Id] = @Id
END
GO;

CREATE OR ALTER PROCEDURE [dbo].[OrganizationSponsorship_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @SponsoringOrganizationId UNIQUEIDENTIFIER,
    @SponsoringOrganizationUserID UNIQUEIDENTIFIER,
    @SponsoredOrganizationId UNIQUEIDENTIFIER,
    @FriendlyName NVARCHAR(256),
    @OfferedToEmail NVARCHAR(256),
    @PlanSponsorshipType TINYINT,
    @ToDelete BIT,
    @LastSyncDate DATETIME2 (7),
    @ValidUntil DATETIME2 (7),
    @IsAdminInitiated BIT = 0,
    @Notes NVARCHAR(512) = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationSponsorship]
    (
        [Id],
        [SponsoringOrganizationId],
        [SponsoringOrganizationUserID],
        [SponsoredOrganizationId],
        [FriendlyName],
        [OfferedToEmail],
        [PlanSponsorshipType],
        [ToDelete],
        [LastSyncDate],
        [ValidUntil],
        [IsAdminInitiated],
        [Notes]
    )
    VALUES
    (
        @Id,
        @SponsoringOrganizationId,
        @SponsoringOrganizationUserID,
        @SponsoredOrganizationId,
        @FriendlyName,
        @OfferedToEmail,
        @PlanSponsorshipType,
        @ToDelete,
        @LastSyncDate,
        @ValidUntil,
        @IsAdminInitiated,
        @Notes
    )
END
GO;

DROP PROCEDURE IF EXISTS [dbo].[OrganizationSponsorship_CreateMany];
DROP PROCEDURE IF EXISTS [dbo].[OrganizationSponsorship_UpdateMany];
DROP TYPE IF EXISTS [dbo].[OrganizationSponsorshipType] GO;

CREATE TYPE [dbo].[OrganizationSponsorshipType] AS TABLE(
    [Id] UNIQUEIDENTIFIER,
    [SponsoringOrganizationId] UNIQUEIDENTIFIER,
    [SponsoringOrganizationUserID] UNIQUEIDENTIFIER,
    [SponsoredOrganizationId] UNIQUEIDENTIFIER,
    [FriendlyName] NVARCHAR(256),
    [OfferedToEmail] VARCHAR(256),
    [PlanSponsorshipType] TINYINT,
    [LastSyncDate] DATETIME2(7),
    [ValidUntil] DATETIME2(7),
    [ToDelete] BIT,
    [IsAdminInitiated] BIT DEFAULT 0,
    [Notes] NVARCHAR(512) NULL
);
GO;

CREATE PROCEDURE [dbo].[OrganizationSponsorship_CreateMany]
    @OrganizationSponsorshipsInput [dbo].[OrganizationSponsorshipType] READONLY
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationSponsorship]
    (
		[Id],
        [SponsoringOrganizationId],
        [SponsoringOrganizationUserID],
        [SponsoredOrganizationId],
        [FriendlyName],
        [OfferedToEmail],
        [PlanSponsorshipType],
        [ToDelete],
        [LastSyncDate],
        [ValidUntil],
        [IsAdminInitiated],
        [Notes]
    )
SELECT
    OS.[Id],
    OS.[SponsoringOrganizationId],
    OS.[SponsoringOrganizationUserID],
    OS.[SponsoredOrganizationId],
    OS.[FriendlyName],
    OS.[OfferedToEmail],
    OS.[PlanSponsorshipType],
    OS.[ToDelete],
    OS.[LastSyncDate],
    OS.[ValidUntil],
    OS.[IsAdminInitiated],
    OS.[Notes]
FROM
    @OrganizationSponsorshipsInput OS
END
GO;

CREATE PROCEDURE [dbo].[OrganizationSponsorship_UpdateMany]
    @OrganizationSponsorshipsInput [dbo].[OrganizationSponsorshipType] READONLY
AS
BEGIN
    SET NOCOUNT ON

UPDATE
    OS
SET
    [Id] = OSI.[Id],
    [SponsoringOrganizationId] = OSI.[SponsoringOrganizationId],
    [SponsoringOrganizationUserID] = OSI.[SponsoringOrganizationUserID],
    [SponsoredOrganizationId] = OSI.[SponsoredOrganizationId],
    [FriendlyName] = OSI.[FriendlyName],
    [OfferedToEmail] = OSI.[OfferedToEmail],
    [PlanSponsorshipType] = OSI.[PlanSponsorshipType],
    [ToDelete] = OSI.[ToDelete],
    [LastSyncDate] = OSI.[LastSyncDate],
    [ValidUntil] = OSI.[ValidUntil],
    [IsAdminInitiated] = OSI.[IsAdminInitiated],
    [Notes] = OSI.[Notes]
FROM
    [dbo].[OrganizationSponsorship] OS
    INNER JOIN
    @OrganizationSponsorshipsInput OSI ON OS.Id = OSI.Id

END
GO;
