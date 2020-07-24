-- Perform any drops needed to start building out PlanType tables and data
IF COL_LENGTH('[dbo].[Organization]', 'PlanTypeId') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Organization]
    DROP CONSTRAINT FK_Organization_PlanType
    ALTER TABLE [dbo].[Organization]
    DROP COLUMN [PlanTypeId];
END
GO

IF OBJECT_ID('[dbo].[PlanType]') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[PlanType]
END
GO

IF OBJECT_ID('[dbo].[PlanTypeGroup]') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[PlanTypeGroup]
END
GO

IF OBJECT_ID('[dbo].[PlanType_Read]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[PlanType_Read]
END
GO

-- Create dbo.PlanTypeGroup
IF OBJECT_ID('[dbo].[PlanTypeGroup]') IS NULL
BEGIN
    CREATE TABLE [dbo].[PlanTypeGroup](
        [Id]                            [INT]               IDENTITY(1,1) NOT NULL,
        [Name]                          [NVARCHAR](50)      NULL,
        [Description]                   [NVARCHAR](max)     NULL,
        [CanBeUsedByBusiness]           [BIT]               NOT NULL,
        [BaseSeats]                     [INT]               NOT NULL,
        [BaseStorageGb]                 [INT]               NOT NULL,
        [MaxCollections]                [INT]               NULL,
        [HasAdditionalSeatsOption]      [BIT]               NOT NULL,
        [HasAdditionalStorageOption]    [BIT]               NOT NULL,
        [HasPremiumAccessAddon]         [BIT]               NOT NULL,
        [TrialPeriodDays]               [INT]               NOT NULL,
        [HasSelfHost]                   [BIT]               NOT NULL,
        [HasPolicies]                   [BIT]               NOT NULL,
        [HasGroups]                     [BIT]               NOT NULL,
        [HasDirectory]                  [BIT]               NOT NULL,
        [HasEvents]                     [BIT]               NOT NULL,
        [HasTotp]                       [BIT]               NOT NULL,
        [Has2fa]                        [BIT]               NOT NULL,
        [HasApi]                        [BIT]               NOT NULL,
        [UsersGetPremium]               [BIT]               NOT NULL,
        [HasSso]                        [bit]               NOT NULL,
        [SortOrder]                     [INT]               NOT NULL,
        [IsLegacy]                      [BIT]               NOT NULL
        CONSTRAINT [PK_PlanTypeGroup] PRIMARY KEY CLUSTERED ([Id] ASC)
    )
END
GO

-- Populate dbo.PlanTypeGroup
IF OBJECT_ID('[dbo].[PlanTypeGroup]') IS NOT NULL
BEGIN
    INSERT INTO [dbo].[PlanTypeGroup]
    VALUES 
        ('Free','For testing or personal users to share with 1 other user.',0,2,0,2,0,0,0,0,0,0,0,0,0,0,0,0,0,0,-1,0),
        ('Families (2019)','For personal use, to share with family & friends.',0,5,1,null,0,1,1,7,1,0,0,0,0,1,0,0,0,0,10,1),
        ('Teams (2019)','For businesses and other team organizations.',1,5,1,null,1,1,0,7,0,0,0,0,0,1,0,0,0,0,20,1),
        ('Enterprise (2019)','For businesses and other large organizations.',1,0,1,null,1,1,0,7,1,1,1,1,1,1,1,1,1,0,30,1),
        ('Families','For personal use, to share with family & friends.',0,5,1,null,0,1,0,7,1,0,0,0,0,1,0,0,0,0,10,0),
        ('Teams','For businesses and other team organizations.',1,0,1,null,1,1,0,7,0,0,0,0,0,1,0,0,0,0,20,0),
        ('Enterprise','For businesses and other large organizations.',1,0,1,null,1,1,0,7,1,1,1,1,1,1,1,1,1,1,30,0)
END
GO

-- Create dbo.PlanType
IF OBJECT_ID('[dbo].[PlanType]') IS NULL
BEGIN
    -- ID increments from 0 for backwards compatability with existing enum
    CREATE TABLE [dbo].[PlanType] (
        [Id]                            [INT]               IDENTITY(0,1) NOT NULL,
        [StripePlanId]                  [NVARCHAR](50)      NULL,
        [StripeSeatPlanId]              [NVARCHAR](50)      NULL,
        [StripeStoragePlanId]           [NVARCHAR](50)      NULL,
        [StripePremiumAccessPlanId]     [NVARCHAR](50)      NULL,
        [BasePrice]                     [DECIMAL](18, 2)    NULL,
        [SeatPrice]                     [DECIMAL](18, 2)    NULL,
        [AdditionalStoragePricePerGb]   [DECIMAL](18, 2)    NULL,
        [HasPremiumAccessAddonCost]     [DECIMAL](18, 2)    NULL,
        [IsAnnual]                      [BIT]               NOT NULL,
        [PlanTypeGroupId]               [INT]               NOT NULL,
        CONSTRAINT [PK_PlanType] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PlanType_PlanTypeGroup] FOREIGN KEY ([PlanTypeGroupId]) REFERENCES [dbo].[PlanTypeGroup] ([Id])
    )
END
GO

-- Populate dbo.PlanType
IF OBJECT_ID('[dbo].[PlanType]') IS NOT NULL
BEGIN
    INSERT INTO [dbo].[PlanType]
    VALUES
        (null,null,null,null,null,null,null,null,0,1),
        ('personal-org-annually', null,'storage-gb-annually','personal-org-premium-access-annually',12, null, 3.96, 3.33,1,2),
        ('teams-org-monthly', 'teams-org-seat-monthly', 'storage-gb-monthly', null, 8, 2.5, 0.5, null, 0, 3),
        ('teams-org-annually', 'teams-org-seat-annually', 'storage-gb-annually', null, 60, 24, 3.96, null, 1, 3),
        (null, 'enterprise-org-seat-monthly', 'storage-gb-monthly', null, null, 4, 0.5, null, 0, 4),
        (null, 'enterprise-org-seat-annually', 'storage-gb-annually', null, null, 36, 3.96, null, 1, 4),
        (null, '2020-teams-org-seat-monthly', 'storage-gb-monthly', null, null, 4, 0.5, null, 0, 3),
        (null, '2020-teams-org-seat-annually', 'storage-gb-annually', null, null, 36, 3.96, null, 1, 3),
        (null, '2020-enterprise-org-seat-monthly', 'storage-gb-monthly', null, null, 6, 0.5, null, 0, 4),
        (null, '2020-enterprise-org-seat-annually', 'storage-gb-annually', null, null, 60, 3.96, null, 1, 4)
END
GO

-- Add PlanTypeId FK to dbo.Organization & Populate existing values based on PlanType's value
IF COL_LENGTH('[dbo].[Organization]', 'PlanTypeId') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[Organization]
    ADD
        [PlanTypeId] INT;

    ALTER TABLE
        [dbo].[Organization]
    ADD CONSTRAINT FK_Organization_PlanType
    FOREIGN KEY (PlanTypeId) REFERENCES PlanType(Id);
END
GO

UPDATE [dbo].[Organization]
SET [PlanTypeId] = [PlanType]
GO

-- Update relevant org procedures/views to pull PlanTypeId
IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'OrganizationView')
BEGIN
    DROP VIEW [dbo].[OrganizationView];
END
GO

CREATE VIEW [dbo].[OrganizationView]
AS
SELECT
    *
FROM
    [dbo].[Organization]
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
    @RevisionDate DATETIME2(7),
    @PlanTypeId INT
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
        [RevisionDate],
        [PlanTypeId]
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
        @RevisionDate,
        @PlanTypeId
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
    @RevisionDate DATETIME2(7),
    @PlanTypeId INT
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
        [RevisionDate] = @RevisionDate,
        [PlanTypeId] = @PlanType
    WHERE
        [Id] = @Id
END
GO


-- Create PlanTypes view
IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'PlanTypePlanTypeGroup')
BEGIN
    DROP VIEW [dbo].[PlanTypePlanTypeGroup];
END
GO

CREATE VIEW [dbo].[PlanTypePlanTypeGroup]
AS
SELECT
    PT.Id,
    PT.StripePlanId,
    PT.StripeSeatPlanId,
    PT.StripeStoragePlanId,
    PT.StripePremiumAccessPlanId,
    PT.BasePrice,
    PT.SeatPrice,
    PT.AdditionalStoragePricePerGb,
    PT.HasPremiumAccessAddonCost,
    PT.IsAnnual,
    PTG.Id AS PlanTypeGroupId,
    PTG.Name,
    PTG.Description,
    PTG.CanBeUsedByBusiness,
    PTG.BaseSeats,
    PTG.BaseStorageGb,
    PTG.MaxCollections,
    PTG.HasAdditionalSeatsOption,
    PTG.HasAdditionalStorageOption,
    PTG.HasPremiumAccessAddon,
    PTG.TrialPeriodDays,
    PTG.HasSelfHost,
    PTG.HasPolicies,
    PTG.HasGroups,
    PTG.HasDirectory,
    PTG.HasEvents,
    PTG.HasTotp,
    PTG.Has2fa,
    PTG.HasApi,
    PTG.UsersGetPremium,
    PTG.HasSso,
    PTG.SortOrder,
    PTG.IsLegacy
FROM
    [dbo].[PlanType] PT
INNER JOIN
    [dbo].[PlanTypeGroup] PTG ON PTG.[Id] = PT.[PlanTypeGroupId]
GO

-- Create PlanTypes_Read procedure
IF OBJECT_ID('[dbo].[PlanTypePlanTypeGroup_Read]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[PlanTypePlanTypeGroup_Read]
END
GO

CREATE PROCEDURE [dbo].[PlanTypePlanTypeGroup_Read]
AS
BEGIN
    SELECT 
        Id,
        StripePlanId,
        StripeSeatPlanId,
        StripeStoragePlanId,
        StripePremiumAccessPlanId,
        BasePrice,
        SeatPrice,
        AdditionalStoragePricePerGb,
        HasPremiumAccessAddonCost,
        IsAnnual,
        PlanTypeGroupId AS "PlanTypeGroup.Id",
        Name AS "PlanTypeGroup.Name",
        Description AS "PlanTypeGroup.Description",
        CanBeUsedByBusiness AS "PlanTypeGroup.CanBeUsedByBusiness",
        BaseSeats AS "PlanTypeGroup.BaseSeats",
        BaseStorageGb AS "PlanTypeGroup.BaseStorageGb",
        MaxCollections AS "PlanTypeGroup.MaxCollections",
        HasAdditionalSeatsOption AS "PlanTypeGroup.HasAdditionalSeatsOption",
        HasAdditionalStorageOption AS "PlanTypeGroup.HasAdditionalStorageOption",
        HasPremiumAccessAddon AS "PlanTypeGroup.HasPremiumAccessAddon",
        TrialPeriodDays AS "PlanTypeGroup.TrialPeriodDays",
        HasSelfHost AS "PlanTypeGroup.HasSelfHost",
        HasPolicies AS "PlanTypeGroup.HasPolicies",
        HasGroups AS "PlanTypeGroup.HasGroups",
        HasDirectory AS "PlanTypeGroup.HasDirectory",
        HasEvents AS "PlanTypeGroup.HasEvents",
        HasTotp AS "PlanTypeGroup.HasTotp",
        Has2fa AS "PlanTypeGroup.Has2fa",
        HasApi AS "PlanTypeGroup.HasApi",
        UsersGetPremium AS "PlanTypeGroup.UsersGetPremium",
        HasSso AS "PlanTypeGroup.HasSso",
        SortOrder AS "PlanTypeGroup.SortOrder",
        IsLegacy AS "PlanTypeGroup.IsLegacy"
    FROM [dbo].[PlanTypePlanTypeGroup]
    FOR JSON PATH
END
GO
