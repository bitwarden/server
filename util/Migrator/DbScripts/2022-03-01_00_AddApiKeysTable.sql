-- Create Organization ApiKey table
IF OBJECT_ID('[dbo].[OrganizationApiKey]') IS NULL
BEGIN
CREATE TABLE [dbo].[OrganizationApiKey] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]    UNIQUEIDENTIFIER NOT NULL,
    [Type]              TINYINT NOT NULL,
    [ApiKey]            VARCHAR(30) NOT NULL,
    [RevisionDate]      DATETIME2(7) NOT NULL,
    CONSTRAINT [PK_OrganizationApiKey] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationApiKey_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
);
END
GO

-- Create indexes for OrganizationApiKey
IF NOT EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_OrganizationApiKey_OrganizationId')
BEGIN
CREATE NONCLUSTERED INDEX [IX_OrganizationApiKey_OrganizationId]
    ON [dbo].[OrganizationApiKey]([OrganizationId] ASC);
END
GO

IF NOT EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_OrganizationApiKey_ApiKey')
BEGIN
CREATE NONCLUSTERED INDEX [IX_OrganizationApiKey_ApiKey]
    ON [dbo].[OrganizationApiKey]([ApiKey] ASC);
END
GO

IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'OrganizationApiKeyView')
BEGIN
    DROP VIEW [dbo].[OrganizationApiKeyView];
END
GO

CREATE VIEW [dbo].[OrganizationApiKeyView]
AS
SELECT
    *
FROM
    [dbo].[OrganizationApiKey]
GO

IF OBJECT_ID('[dbo].[OrganizationApiKey_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationApiKey_Create]
END
GO

CREATE PROCEDURE [dbo].[OrganizationApiKey_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ApiKey VARCHAR(30),
    @Type TINYINT,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationApiKey]
    (
        [Id],
        [OrganizationId],
        [ApiKey],
        [Type],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @ApiKey,
        @Type,
        @RevisionDate
    )
END
GO

IF OBJECT_ID('[dbo].[OrganizationApiKey_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationApiKey_Update]
END
GO

CREATE PROCEDURE [dbo].[OrganizationApiKey_Update]
    @Id UNIQUEIDENTIFIER,
    @ApiKey VARCHAR(30),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationApiKey]
    SET
        [ApiKey] = @ApiKey,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[OrganizationApiKey_ReadManuByOrganizationId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationApiKey_ReadManyByOrganizationId]
END
GO

CREATE PROCEDURE [dbo].[OrganizationApiKey_ReadManyByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationApiKeyView]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[OrganizationApiKey_ReadByOrganizationIdType]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationApiKey_ReadByOrganizationIdType]
END
GO

CREATE PROCEDURE [dbo].[OrganizationApiKey_ReadByOrganizationIdType]
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationApiKeyView]
    WHERE
        [OrganizationId] = @OrganizationId AND
        [Type] = @Type
END
GO

IF OBJECT_ID('[dbo].[OrganizationApiKey_OrganizationDeleted]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationApiKey_OrganizationDeleted]
END
GO

CREATE PROCEDURE [dbo].[OrganizationApiKey_OrganizationDeleted]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationApiKey]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO

-- Update Organization delete sprocs to handle organization api key
IF OBJECT_ID('[dbo].[Organization_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_DeleteById]
END
GO

PRINT N'Creating GenerateComb Function'
GO

CREATE OR ALTER FUNCTION [dbo].[GenerateComb] (@time DATETIME, @uuid UNIQUEIDENTIFIER)
RETURNS UNIQUEIDENTIFIER
AS
BEGIN
    DECLARE @comb UNIQUEIDENTIFIER;

    SELECT @comb = CAST(
        CAST(@uuid AS BINARY(10)) +
        CAST(@time AS BINARY(6))
    AS UNIQUEIDENTIFIER);

    RETURN @comb
END;
GO

IF COL_LENGTH('[dbo].[Organization]', 'ApiKey') IS NOT NULl
BEGIN
    BEGIN TRANSACTION MigrateOrganizationApiKeys
    PRINT N'Migrating Organization ApiKeys'

    INSERT INTO [dbo].[OrganizationApiKey]
        (
            [Id],
            [OrganizationId], 
            [ApiKey], 
            [Type],
            [RevisionDate]
        )
        SELECT
            [dbo].[GenerateComb]([CreationDate], NEWID()),
            [Id] AS [OrganizationId], 
            [ApiKey],
            0 AS [Type], -- 0 represents 'Default' type
            [RevisionDate]
        FROM [dbo].[Organization]

    PRINT N'Dropping old column'
    ALTER TABLE
        [dbo].[Organization]
    DROP COLUMN
        [ApiKey]

    COMMIT TRANSACTION MigrateOrganizationApiKeys;
END
GO

PRINT N'Deleting GenerateComb function'
DROP FUNCTION [dbo].[GenerateComb];
GO


IF OBJECT_ID('[dbo].[Organization_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_Create]
END
GO

CREATE PROCEDURE [dbo].[Organization_Create]
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
    @UseKeyConnector BIT = 0
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
        [UseKeyConnector]
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
        @UseKeyConnector
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
    @UseKeyConnector BIT = 0
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
        [UseKeyConnector] = @UseKeyConnector
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[OrganizationView]') IS NOT NULL
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

IF OBJECT_ID('[dbo].[OrganizationConnection]') IS NULL
BEGIN
CREATE TABLE [dbo].[OrganizationConnection] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]    UNIQUEIDENTIFIER NOT NULL,
    [Type]              TINYINT NOT NULL,
    [Enabled]           BIT NOT NULL,
    [Config]            NVARCHAR (MAX) NULL,
    CONSTRAINT [PK_OrganizationConnection] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationConnection_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
)
END

-- Create indexes for OrganizationConnection
IF NOT EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_OrganizationConnection_OrganizationId')
BEGIN
CREATE NONCLUSTERED INDEX [IX_OrganizationConnection_OrganizationId]
    ON [dbo].[OrganizationConnection]([OrganizationId] ASC);
END

-- Create View
IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'OrganizationConnectionView')
BEGIN
    DROP VIEW [dbo].[OrganizationConnectionView]
END
GO

CREATE VIEW [dbo].[OrganizationConnectionView]
AS
SELECT
    *
FROM
    [dbo].[OrganizationConnection]
GO

-- Create Stored Procedures
IF OBJECT_ID('[dbo].[OrganizationConnection_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationConnection_ReadById];
END
GO

CREATE PROCEDURE [dbo].[OrganizationConnection_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationConnectionView]
    WHERE
        [Id] = @Id
END
GO


IF OBJECT_ID('[dbo].[OrganizationConnection_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationConnection_Create]
END
GO

CREATE PROCEDURE [dbo].[OrganizationConnection_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Enabled BIT,
    @Config NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationConnection]
    (
        [Id],
        [OrganizationId],
        [Type],
        [Enabled],
        [Config]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Type,
        @Enabled,
        @Config
    )
END
GO

IF OBJECT_ID('[dbo].[OrganizationConnection_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationConnection_Update]
END
GO

CREATE PROCEDURE [dbo].[OrganizationConnection_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Enabled BIT,
    @Config NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationConnection]
    SET
        [OrganizationId] = @OrganizationId,
        [Type] = @Type,
        [Enabled] = @Enabled,
        [Config] = @Config
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[OrganizationConnection_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationConnection_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[OrganizationConnection_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE FROM
        [dbo].[OrganizationConnection]
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[OrganizationConnection_OrganizationDeleted]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationConnection_OrganizationDeleted]
END
GO

CREATE PROCEDURE [dbo].[OrganizationConnection_OrganizationDeleted]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE FROM
        [dbo].[OrganizationConnection]
    WHERE
        [OrganizationId] = @Id
END
GO

IF OBJECT_ID('[dbo].[OrganizationConnection_ReadEnabledByOrganizationIdType]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationConnection_ReadEnabledByOrganizationIdType];
END
GO

CREATE PROCEDURE [dbo].[OrganizationConnection_ReadEnabledByOrganizationIdType]
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationConnectionView]
    WHERE
        [OrganizationId] = @OrganizationId AND
        [Type] = @Type AND
        [Enabled] = 1
END
GO

CREATE PROCEDURE [dbo].[Organization_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @Id

    DECLARE @BatchSize INT = 100
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION Organization_DeleteById_Ciphers

        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [UserId] IS NULL
            AND [OrganizationId] = @Id

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION Organization_DeleteById_Ciphers
    END

    BEGIN TRANSACTION Organization_DeleteById

    DELETE
    FROM
        [dbo].[SsoUser]
    WHERE
        [OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[SsoConfig]
    WHERE
        [OrganizationId] = @Id

    DELETE CU
    FROM 
        [dbo].[CollectionUser] CU
    INNER JOIN 
        [dbo].[OrganizationUser] OU ON [CU].[OrganizationUserId] = [OU].[Id]
    WHERE 
        [OU].[OrganizationId] = @Id

    DELETE
    FROM 
        [dbo].[OrganizationUser]
    WHERE 
        [OrganizationId] = @Id

    DELETE
    FROM
         [dbo].[ProviderOrganization]
    WHERE
        [OrganizationId] = @Id

    EXEC [dbo].[OrganizationApiKey_OrganizationDeleted] @Id
    EXEC [dbo].[OrganizationConnection_OrganizationDeleted] @Id

    DELETE
    FROM
        [dbo].[Organization]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION Organization_DeleteById
END
GO

IF COL_LENGTH('[dbo].[OrganizationSponsorship]', 'TimesRenewedWithoutValidation') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[OrganizationSponsorship] DROP CONSTRAINT DF__Organizat__Times__2B2A60FE
    ALTER TABLE [dbo].[OrganizationSponsorship] DROP COLUMN [TimesRenewedWithoutValidation]
END

IF COL_LENGTH('[dbo].[OrganizationSponsorship]', 'SponsorshipLapsedDate') IS NOT NULL
BEGIN
    EXEC sp_rename '[dbo].[OrganizationSponsorship].[SponsorshipLapsedDate]', 'ValidUntil'
END

IF COL_LENGTH('[dbo].[OrganizationSponsorship]', 'CloudSponsor') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[OrganizationSponsorship] DROP COLUMN [CloudSponsor]
END

IF COL_LENGTH('[dbo].[OrganizationSponsorship]', 'ToDelete') IS NULL
BEGIN
    ALTER TABLE [dbo].[OrganizationSponsorship] ADD [ToDelete] BIT NOT NULL DEFAULT(0)
END

IF EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_OrganizationSponsorship_InstallationId')
BEGIN
    DROP INDEX [IX_OrganizationSponsorship_InstallationId]
        ON [dbo].[OrganizationSponsorship]
END
GO

IF COL_LENGTH('[dbo].[OrganizationSponsorship]', 'InstallationId') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[OrganizationSponsorship] DROP CONSTRAINT [FK_OrganizationSponsorship_InstallationId]
    ALTER TABLE [dbo].[OrganizationSponsorship] DROP COLUMN [InstallationId]
END

-- Remake View
IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'OrganizationSponsorshipView')
BEGIN
    DROP VIEW [dbo].[OrganizationSponsorshipView];
END
GO

CREATE VIEW [dbo].[OrganizationSponsorshipView]
AS
SELECT
    *
FROM
    [dbo].[OrganizationSponsorship]
GO

IF OBJECT_ID('[dbo].[OrganizationSponsorship_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSponsorship_Create]
END
GO

-- OrganizationSponsorship_Create
IF OBJECT_ID('[dbo].[OrganizationSponsorship_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSponsorship_Create]
END
GO

CREATE PROCEDURE [dbo].[OrganizationSponsorship_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @SponsoringOrganizationId UNIQUEIDENTIFIER,
    @SponsoringOrganizationUserID UNIQUEIDENTIFIER,
    @SponsoredOrganizationId UNIQUEIDENTIFIER,
    @FriendlyName NVARCHAR(256),
    @OfferedToEmail NVARCHAR(256),
    @PlanSponsorshipType TINYINT,
    @ToDelete BIT,
    @LastSyncDate DATETIME2 (7),
    @ValidUntil DATETIME2 (7)
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
        [ValidUntil]
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
        @ValidUntil
    )
END
GO

-- OrganizationSponsorship_Update
IF OBJECT_ID('[dbo].[OrganizationSponsorship_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSponsorship_Update]
END
GO

CREATE PROCEDURE [dbo].[OrganizationSponsorship_Update]
    @Id UNIQUEIDENTIFIER,
    @SponsoringOrganizationId UNIQUEIDENTIFIER,
    @SponsoringOrganizationUserID UNIQUEIDENTIFIER,
    @SponsoredOrganizationId UNIQUEIDENTIFIER,
    @FriendlyName NVARCHAR(256),
    @OfferedToEmail NVARCHAR(256),
    @PlanSponsorshipType TINYINT,
    @ToDelete BIT,
    @LastSyncDate DATETIME2 (7),
    @ValidUntil DATETIME2 (7)
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
        [ValidUntil] = @ValidUntil
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[Organization_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[Organization_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @Id

    DECLARE @BatchSize INT = 100
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION Organization_DeleteById_Ciphers

        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [UserId] IS NULL
            AND [OrganizationId] = @Id

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION Organization_DeleteById_Ciphers
    END

    BEGIN TRANSACTION Organization_DeleteById

    DELETE
    FROM
        [dbo].[SsoUser]
    WHERE
        [OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[SsoConfig]
    WHERE
        [OrganizationId] = @Id

    DELETE CU
    FROM 
        [dbo].[CollectionUser] CU
    INNER JOIN 
        [dbo].[OrganizationUser] OU ON [CU].[OrganizationUserId] = [OU].[Id]
    WHERE 
        [OU].[OrganizationId] = @Id

    DELETE
    FROM 
        [dbo].[OrganizationUser]
    WHERE 
        [OrganizationId] = @Id

    DELETE
    FROM
         [dbo].[ProviderOrganization]
    WHERE
        [OrganizationId] = @Id

    EXEC [dbo].[OrganizationApiKey_OrganizationDeleted] @Id
    EXEC [dbo].[OrganizationConnection_OrganizationDeleted] @Id

    DELETE
    FROM
        [dbo].[Organization]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION Organization_DeleteById
END
GO

-- OrganizationSponsorship have a different delete process for whether or not server is SH or not
IF OBJECT_ID('[dbo].[OrganizationSponsorship_OrganizationDeleted]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSponsorship_OrganizationDeleted]
END
GO
