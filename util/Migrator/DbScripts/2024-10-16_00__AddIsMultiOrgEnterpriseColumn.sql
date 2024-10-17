IF COL_LENGTH('[dbo].[OrganizationUser]', 'AccessAll') IS NOT NULL
BEGIN
ALTER TABLE
    [dbo].[Provider]
ADD
    [IsMultiOrgEnterprise] BIT NULL
END
GO

-- Alter 'Provider_Create' SPROC to add 'Gateway', 'GatewayCustomerId' and 'GatewaySubscriptionId' columns.
CREATE OR ALTER PROCEDURE [dbo].[Provider_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @Name NVARCHAR(50),
    @BusinessName NVARCHAR(50),
    @BusinessAddress1 NVARCHAR(50),
    @BusinessAddress2 NVARCHAR(50),
    @BusinessAddress3 NVARCHAR(50),
    @BusinessCountry VARCHAR(2),
    @BusinessTaxNumber NVARCHAR(30),
    @BillingEmail NVARCHAR(256),
    @BillingPhone NVARCHAR(50) = NULL,
    @Status TINYINT,
    @Type TINYINT = 0,
    @UseEvents BIT,
    @Enabled BIT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Gateway TINYINT = 0,
    @GatewayCustomerId VARCHAR(50) = NULL,
    @GatewaySubscriptionId VARCHAR(50) = NULL,
    @IsMultiOrgEnterprise BIT = NULL
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
        [RevisionDate],
        [Gateway],
        [GatewayCustomerId],
        [GatewaySubscriptionId],
        [IsMultiOrgEnterprise]
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
        @RevisionDate,
        @Gateway,
        @GatewayCustomerId,
        @GatewaySubscriptionId,
        @IsMultiOrgEnterprise
    )
END
GO

-- Alter 'Provider_Update' SPROC to add 'IsMultiOrgEnterprise' column.
CREATE OR ALTER PROCEDURE [dbo].[Provider_Update]
    @Id UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @BusinessName NVARCHAR(50),
    @BusinessAddress1 NVARCHAR(50),
    @BusinessAddress2 NVARCHAR(50),
    @BusinessAddress3 NVARCHAR(50),
    @BusinessCountry VARCHAR(2),
    @BusinessTaxNumber NVARCHAR(30),
    @BillingEmail NVARCHAR(256),
    @BillingPhone NVARCHAR(50) = NULL,
    @Status TINYINT,
    @Type TINYINT = 0,
    @UseEvents BIT,
    @Enabled BIT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Gateway TINYINT = 0,
    @GatewayCustomerId VARCHAR(50) = NULL,
    @GatewaySubscriptionId VARCHAR(50) = NULL,
    @IsMultiOrgEnterprise BIT = NULL
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
    [RevisionDate] = @RevisionDate,
    [Gateway] = @Gateway,
    [GatewayCustomerId] = @GatewayCustomerId,
    [GatewaySubscriptionId] = @GatewaySubscriptionId,
    [IsMultiOrgEnterprise] = @IsMultiOrgEnterprise
WHERE
    [Id] = @Id
END
GO

-- Refresh modules for SPROCs reliant on 'Provider' table/view.
IF OBJECT_ID('[dbo].[Provider_ReadAbilities]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[Provider_ReadAbilities]';
END
GO

IF OBJECT_ID('[dbo].[Provider_ReadById]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[Provider_ReadById]';
END
GO

IF OBJECT_ID('[dbo].[Provider_ReadByOrganizationId]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[Provider_ReadByOrganizationId]';
END
GO

IF OBJECT_ID('[dbo].[Provider_Search]') IS NOT NULL
BEGIN
EXECUTE sp_refreshsqlmodule N'[dbo].[Provider_Search]';
END
GO
