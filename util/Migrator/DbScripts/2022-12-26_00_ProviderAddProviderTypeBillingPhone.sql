-- Add column 'Type' to 'Provider' table
IF COL_LENGTH('[dbo].[Provider]', 'Type') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[Provider]
    ADD
        [Type] TINYINT NOT NULL CONSTRAINT DF_Provider_Type DEFAULT (0);
END
GO

-- Add column 'BillingPhone' to 'Provider' table
IF COL_LENGTH('[dbo].[Provider]', 'BillingPhone') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[Provider]
    ADD
        [BillingPhone] NVARCHAR (50) NULL
END
GO

-- Recreate ProviderView so that it includes the new columns 'Type' and 'BillingPhone'
IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'ProviderView')
    BEGIN
        DROP VIEW [dbo].[ProviderView]
    END
GO

CREATE VIEW [dbo].[ProviderView]
AS
SELECT
    *
FROM
    [dbo].[Provider]
GO

-- Recreate ProviderUserProviderDetailsView so that it includes the new columns 'Type' and 'BillingPhone'
IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'ProviderUserProviderDetailsView')
    BEGIN
        DROP VIEW [dbo].[ProviderUserProviderDetailsView]
    END
GO

CREATE VIEW [dbo].[ProviderUserProviderDetailsView]
AS
SELECT
    PU.[UserId],
    PU.[ProviderId],
    P.[Name],
    PU.[Key],
    PU.[Status],
    PU.[Type],
    P.[Enabled],
    PU.[Permissions],
    P.[UseEvents],
    P.[Status] ProviderStatus,
    P.[Type] ProviderType
FROM
    [dbo].[ProviderUser] PU
    LEFT JOIN
    [dbo].[Provider] P ON P.[Id] = PU.[ProviderId]
GO

-- Alter Provider_Create view to add new columns 'Type' and 'BillingPhone'
ALTER PROCEDURE [dbo].[Provider_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @Name NVARCHAR(50),
    @BusinessName NVARCHAR(50),
    @BusinessAddress1 NVARCHAR(50),
    @BusinessAddress2 NVARCHAR(50),
    @BusinessAddress3 NVARCHAR(50),
    @BusinessCountry VARCHAR(2),
    @BusinessTaxNumber NVARCHAR(30),
    @BillingEmail NVARCHAR(256),
    @BillingPhone NVARCHAR(50),
    @Status TINYINT,
    @Type TINYINT,
    @UseEvents BIT,
    @Enabled BIT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Provider]
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
        [BillingPhone],
        [Status],
        [Type],
        [UseEvents],
        [Enabled],
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
        @BillingPhone,
        @Status,
        @Type,
        @UseEvents,
        @Enabled,
        @CreationDate,
        @RevisionDate
    )
END
GO

-- Alter Provider_Update view to add new columns 'Type' and 'BillingPhone'
ALTER PROCEDURE [dbo].[Provider_Update]
    @Id UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @BusinessName NVARCHAR(50),
    @BusinessAddress1 NVARCHAR(50),
    @BusinessAddress2 NVARCHAR(50),
    @BusinessAddress3 NVARCHAR(50),
    @BusinessCountry VARCHAR(2),
    @BusinessTaxNumber NVARCHAR(30),
    @BillingEmail NVARCHAR(256),
    @BillingPhone NVARCHAR(50),
    @Status TINYINT,
    @Type TINYINT,
    @UseEvents BIT,
    @Enabled BIT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

UPDATE
    [dbo].[Provider]
SET
    [Name] = @Name,
    [BusinessName] = @BusinessName,
    [BusinessAddress1] = @BusinessAddress1,
    [BusinessAddress2] = @BusinessAddress2,
    [BusinessAddress3] = @BusinessAddress3,
    [BusinessCountry] = @BusinessCountry,
    [BusinessTaxNumber] = @BusinessTaxNumber,
    [BillingEmail] = @BillingEmail,
    [BillingPhone] = @BillingPhone,
    [Status] = @Status,
    [Type] = @Type,
    [UseEvents] = @UseEvents,
    [Enabled] = @Enabled,
    [CreationDate] = @CreationDate,
    [RevisionDate] = @RevisionDate
WHERE
    [Id] = @Id
END
