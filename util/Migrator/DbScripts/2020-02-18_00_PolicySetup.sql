IF OBJECT_ID('[dbo].[Policy]') IS NULL
BEGIN
    CREATE TABLE [dbo].[Policy] (
        [Id]             UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
        [Type]           TINYINT          NOT NULL,
        [Data]           NVARCHAR (MAX)   NULL,
        [Enabled]        BIT              NOT NULL,
        [CreationDate]   DATETIME2 (7)    NOT NULL,
        [RevisionDate]   DATETIME2 (7)    NOT NULL,
        CONSTRAINT [PK_Policy] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Policy_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_Policy_OrganizationId_Type]
        ON [dbo].[Policy]([OrganizationId] ASC, [Type] ASC);
END
GO

IF OBJECT_ID('[dbo].[Policy_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Policy_Create]
END
GO

CREATE PROCEDURE [dbo].[Policy_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Enabled BIT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Policy]
    (
        [Id],
        [OrganizationId],
        [Type],
        [Data],
        [Enabled],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Type,
        @Data,
        @Enabled,
        @CreationDate,
        @RevisionDate
    )

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[Policy_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Policy_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[Policy_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrganizationId UNIQUEIDENTIFIER = (SELECT TOP 1 [OrganizationId] FROM [dbo].[Policy] WHERE [Id] = @Id)
    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END

    DELETE
    FROM
        [dbo].[Policy]
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[Policy_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Policy_ReadById]
END
GO

CREATE PROCEDURE [dbo].[Policy_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[PolicyView]
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[Policy_ReadByOrganizationId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Policy_ReadByOrganizationId]
END
GO

CREATE PROCEDURE [dbo].[Policy_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[PolicyView]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[Policy_ReadByOrganizationIdType]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Policy_ReadByOrganizationIdType]
END
GO

CREATE PROCEDURE [dbo].[Policy_ReadByOrganizationIdType]
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        *
    FROM
        [dbo].[PolicyView]
    WHERE
        [OrganizationId] = @OrganizationId
        AND [Type] = @Type
END
GO

IF OBJECT_ID('[dbo].[Policy_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Policy_Update]
END
GO

CREATE PROCEDURE [dbo].[Policy_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Enabled BIT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Policy]
    SET
        [OrganizationId] = @OrganizationId,
        [Type] = @Type,
        [Data] = @Data,
        [Enabled] = @Enabled,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
GO

IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'PolicyView')
BEGIN
    DROP VIEW [dbo].[PolicyView]
END
GO

CREATE VIEW [dbo].[PolicyView]
AS
SELECT
    *
FROM
    [dbo].[Policy]
GO

IF COL_LENGTH('[dbo].[Event]', 'PolicyId') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[Event]
    ADD
        [PolicyId] UNIQUEIDENTIFIER NULL
END
GO

IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'EventView')
BEGIN
    DROP VIEW [dbo].[EventView]
END
GO

CREATE VIEW [dbo].[EventView]
AS
SELECT
    *
FROM
    [dbo].[Event]
GO

IF OBJECT_ID('[dbo].[Event_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Event_Create]
END
GO

CREATE PROCEDURE [dbo].[Event_Create]
    @Id UNIQUEIDENTIFIER,
    @Type INT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @CollectionId UNIQUEIDENTIFIER,
    @PolicyId UNIQUEIDENTIFIER,
    @GroupId UNIQUEIDENTIFIER,
    @OrganizationUserId UNIQUEIDENTIFIER,
    @ActingUserId UNIQUEIDENTIFIER,
    @DeviceType SMALLINT,
    @IpAddress VARCHAR(50),
    @Date DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Event]
    (
        [Id],
        [Type],
        [UserId],
        [OrganizationId],
        [CipherId],
        [CollectionId],
        [PolicyId],
        [GroupId],
        [OrganizationUserId],
        [ActingUserId],
        [DeviceType],
        [IpAddress],
        [Date]
    )
    VALUES
    (
        @Id,
        @Type,
        @UserId,
        @OrganizationId,
        @CipherId,
        @CollectionId,
        @PolicyId,
        @GroupId,
        @OrganizationUserId,
        @ActingUserId,
        @DeviceType,
        @IpAddress,
        @Date
    )
END
GO

IF COL_LENGTH('[dbo].[Organization]', 'UsePolicies') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[Organization]
    ADD
        [UsePolicies] BIT NULL
END
GO

UPDATE
    [dbo].[Organization]
SET
    [UsePolicies] = (CASE WHEN [PlanType] = 5 OR [PlanType] = 4 THEN 1 ELSE 0 END)
WHERE
    [UsePolicies] IS NULL
GO

ALTER TABLE
    [dbo].[Organization]
ALTER COLUMN
    [UsePolicies] BIT NOT NULL
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

IF OBJECT_ID('[dbo].[Policy_ReadByUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Policy_ReadByUserId]
END
GO

CREATE PROCEDURE [dbo].[Policy_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        P.*
    FROM
        [dbo].[PolicyView] P
    INNER JOIN
        [dbo].[OrganizationUser] OU ON P.[OrganizationId] = OU.[OrganizationId]
    INNER JOIN
        [dbo].[Organization] O ON OU.[OrganizationId] = O.[Id]
    WHERE
        OU.[UserId] = @UserId
        AND OU.[Status] = 2 -- 2 = Confirmed
        AND O.[Enabled] = 1
END
GO
