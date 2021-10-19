IF COL_LENGTH('[dbo].[Organization]', 'UseSso') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[Organization]
    ADD
        [UseSso] BIT NULL
END
GO

UPDATE
    [dbo].[Organization]
SET
    [UseSso] = (CASE WHEN [PlanType] = 10 OR [PlanType] = 11 THEN 1 ELSE 0 END)
WHERE
    [UseSso] IS NULL
GO

ALTER TABLE
    [dbo].[Organization]
ALTER COLUMN
    [UseSso] BIT NOT NULL
GO

IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'OrganizationView')
BEGIN
    DROP VIEW [dbo].[OrganizationView]
END
GO

CREATE VIEW [dbo].[OrganizationView]
AS
SELECT
    *
FROM
    [dbo].[Organization]
GO

IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'OrganizationUserOrganizationDetailsView')
BEGIN
    DROP VIEW [dbo].[OrganizationUserOrganizationDetailsView]
END
GO

CREATE VIEW [dbo].[OrganizationUserOrganizationDetailsView]
AS
SELECT
    OU.[UserId],
    OU.[OrganizationId],
    O.[Name],
    O.[Enabled],
    O.[UsePolicies],
    O.[UseSso],
    O.[UseGroups],
    O.[UseDirectory],
    O.[UseEvents],
    O.[UseTotp],
    O.[Use2fa],
    O.[UseApi],
    O.[SelfHost],
    O.[UsersGetPremium],
    O.[Seats],
    O.[MaxCollections],
    O.[MaxStorageGb],
    OU.[Key],
    OU.[Status],
    OU.[Type]
FROM
    [dbo].[OrganizationUser] OU
INNER JOIN
    [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId]
GO

IF OBJECT_ID('[dbo].[Organization_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_Create]
END
GO

CREATE PROCEDURE [dbo].[Organization_Create]
    @Id UNIQUEIDENTIFIER,
    @Identifier NVARCHAR(50),
    @Name NVARCHAR(50),
    @BusinessName NVARCHAR(50),
    @BusinessAddress1 NVARCHAR(50),
    @BusinessAddress2 NVARCHAR(50),
    @BusinessAddress3 NVARCHAR(50),
    @BusinessCountry VARCHAR(2),
    @BusinessTaxNumber NVARCHAR(30),
    @BillingEmail NVARCHAR(50),
    @Plan NVARCHAR(50),
    @PlanType TINYINT,
    @Seats SMALLINT,
    @MaxCollections SMALLINT,
    @UsePolicies BIT,
    @UseSso BIT,
    @UseGroups BIT,
    @UseDirectory BIT,
    @UseEvents BIT,
    @UseTotp BIT,
    @Use2fa BIT,
    @UseApi BIT,
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
    @ApiKey VARCHAR(30),
    @TwoFactorProviders NVARCHAR(MAX),
    @ExpirationDate DATETIME2(7),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
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
        [ApiKey],
        [TwoFactorProviders],
        [ExpirationDate],
        [CreationDate],
        [RevisionDate]
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
        @ApiKey,
        @TwoFactorProviders,
        @ExpirationDate,
        @CreationDate,
        @RevisionDate
    )
END
GO

IF OBJECT_ID('[dbo].[Organization_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_Update]
END
GO

CREATE PROCEDURE [dbo].[Organization_Update]
    @Id UNIQUEIDENTIFIER,
    @Identifier NVARCHAR(50),
    @Name NVARCHAR(50),
    @BusinessName NVARCHAR(50),
    @BusinessAddress1 NVARCHAR(50),
    @BusinessAddress2 NVARCHAR(50),
    @BusinessAddress3 NVARCHAR(50),
    @BusinessCountry VARCHAR(2),
    @BusinessTaxNumber NVARCHAR(30),
    @BillingEmail NVARCHAR(50),
    @Plan NVARCHAR(50),
    @PlanType TINYINT,
    @Seats SMALLINT,
    @MaxCollections SMALLINT,
    @UsePolicies BIT,
    @UseSso BIT,
    @UseGroups BIT,
    @UseDirectory BIT,
    @UseEvents BIT,
    @UseTotp BIT,
    @Use2fa BIT,
    @UseApi BIT,
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
    @ApiKey VARCHAR(30),
    @TwoFactorProviders NVARCHAR(MAX),
    @ExpirationDate DATETIME2(7),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
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
        [ApiKey] = @ApiKey,
        [TwoFactorProviders] = @TwoFactorProviders,
        [ExpirationDate] = @ExpirationDate,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO
