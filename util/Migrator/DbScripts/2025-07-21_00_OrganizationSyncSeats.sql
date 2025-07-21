-- Add the new column if it doesn't exist
IF NOT EXISTS (SELECT 1
           FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_NAME = 'Organization'
             AND COLUMN_NAME = 'SyncSeats')
    BEGIN
        ALTER TABLE [dbo].[Organization]
            ADD [SyncSeats] BIT NOT NULL DEFAULT 0;
    END
GO

-- Refresh view
EXEC sp_refreshsqlmodule N'[dbo].[OrganizationView]';
GO

CREATE OR ALTER PROCEDURE [dbo].[Organization_GetOrganizationsForSubscriptionSync]
AS
BEGIN
    SELECT *
    FROM [dbo].[OrganizationView]
    WHERE [Seats] IS NOT NULL AND [SyncSeats] = 1
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Organization_UpdateSubscriptionStatus]
    @SuccessfulOrganizations NVARCHAR(MAX),
    @SyncDate DATETIME2
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @SuccessfulOrgIds TABLE (Id UNIQUEIDENTIFIER)

    INSERT INTO @SuccessfulOrgIds (Id)
    SELECT [value]
    FROM OPENJSON(@SuccessfulOrganizations)

    UPDATE o
    SET
        [SyncSeats] = 0,
        [RevisionDate] = @SyncDate
    FROM [dbo].[Organization] o
        INNER JOIN @SuccessfulOrgIds success on success.Id = o.Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Organization_IncrementSeatCount]
    @OrganizationId UNIQUEIDENTIFIER,
    @SeatsToAdd INT,
    @RequestDate DATETIME2
AS
BEGIN
    SET NOCOUNT ON

    UPDATE [dbo].[Organization]
    SET
        [Seats] = [Seats] + @SeatsToAdd,
        [SyncSeats] = 1,
        [RevisionDate] = @RequestDate
    WHERE [Id] = @OrganizationId
END
GO

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
    @UseOrganizationDomains BIT = 0,
    @UseAdminSponsoredFamilies BIT = 0,
    @SyncSeats BIT = 0
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
        [UseOrganizationDomains],
        [UseAdminSponsoredFamilies],
        [SyncSeats]
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
            @UseOrganizationDomains,
            @UseAdminSponsoredFamilies,
            @SyncSeats
        )
END
GO

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
    @UseOrganizationDomains BIT = 0,
    @UseAdminSponsoredFamilies BIT = 0,
    @SyncSeats BIT = 0
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
        [UseOrganizationDomains] = @UseOrganizationDomains,
        [UseAdminSponsoredFamilies] = @UseAdminSponsoredFamilies,
        [SyncSeats] = @SyncSeats
    WHERE
        [Id] = @Id
END
GO
