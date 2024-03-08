-- Provider

-- Add 'Gateway' column to 'Provider' table.
IF COL_LENGTH('[dbo].[Provider]', 'Gateway') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[Provider]
    ADD
        [Gateway] TINYINT NULL;
END
GO

-- Add 'GatewayCustomerId' column to 'Provider' table
IF COL_LENGTH('[dbo].[Provider]', 'GatewayCustomerId') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[Provider]
    ADD
        [GatewayCustomerId] VARCHAR (50) NULL;
END
GO

-- Add 'GatewaySubscriptionId' column to 'Provider' table
IF COL_LENGTH('[dbo].[Provider]', 'GatewaySubscriptionId') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[Provider]
    ADD
        [GatewaySubscriptionId] VARCHAR (50) NULL;
END
GO

-- Recreate 'ProviderView' so that it includes the 'Gateway', 'GatewayCustomerId' and 'GatewaySubscriptionId' columns.
CREATE OR ALTER VIEW [dbo].[ProviderView]
AS
SELECT
    *
FROM
    [dbo].[Provider]
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
    @GatewaySubscriptionId VARCHAR(50) = NULL
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
        [GatewaySubscriptionId]
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
        @GatewaySubscriptionId
    )
END
GO

-- Alter 'Provider_Update' SPROC to add 'Gateway', 'GatewayCustomerId' and 'GatewaySubscriptionId' columns.
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
    @GatewaySubscriptionId VARCHAR(50) = NULL
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
    [GatewaySubscriptionId] = @GatewaySubscriptionId
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

-- Transaction

-- Add 'ProviderId' column to 'Transaction' table.
IF COL_LENGTH('[dbo].[Transaction]', 'ProviderId') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[Transaction]
    ADD
        [ProviderId] UNIQUEIDENTIFIER NULL,
    CONSTRAINT
        [FK_Transaction_Provider] FOREIGN KEY ([ProviderId]) REFERENCES [dbo].[Provider] ([Id]) ON DELETE CASCADE;
END
GO

-- Recreate 'TransactionView' so that it includes the 'ProviderId' column.
CREATE OR ALTER VIEW [dbo].[TransactionView]
AS
SELECT
    *
FROM
    [dbo].[Transaction]
GO

-- Alter 'Transaction_Create' SPROC to add 'ProviderId' column.
CREATE OR ALTER PROCEDURE [dbo].[Transaction_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Amount MONEY,
    @Refunded BIT,
    @RefundedAmount MONEY,
    @Details NVARCHAR(100),
    @PaymentMethodType TINYINT,
    @Gateway TINYINT,
    @GatewayId VARCHAR(50),
    @CreationDate DATETIME2(7),
    @ProviderId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Transaction]
    (
        [Id],
        [UserId],
        [OrganizationId],
        [Type],
        [Amount],
        [Refunded],
        [RefundedAmount],
        [Details],
        [PaymentMethodType],
        [Gateway],
        [GatewayId],
        [CreationDate],
        [ProviderId]
    )
    VALUES
    (
        @Id,
        @UserId,
        @OrganizationId,
        @Type,
        @Amount,
        @Refunded,
        @RefundedAmount,
        @Details,
        @PaymentMethodType,
        @Gateway,
        @GatewayId,
        @CreationDate,
        @ProviderId
    )
END
GO

-- Alter 'Transaction_Update' SPROC to add 'ProviderId' column.
CREATE OR ALTER PROCEDURE [dbo].[Transaction_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Amount MONEY,
    @Refunded BIT,
    @RefundedAmount MONEY,
    @Details NVARCHAR(100),
    @PaymentMethodType TINYINT,
    @Gateway TINYINT,
    @GatewayId VARCHAR(50),
    @CreationDate DATETIME2(7),
    @ProviderId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Transaction]
    SET
        [UserId] = @UserId,
        [OrganizationId] = @OrganizationId,
        [Type] = @Type,
        [Amount] = @Amount,
        [Refunded] = @Refunded,
        [RefundedAmount] = @RefundedAmount,
        [Details] = @Details,
        [PaymentMethodType] = @PaymentMethodType,
        [Gateway] = @Gateway,
        [GatewayId] = @GatewayId,
        [CreationDate] = @CreationDate,
        [ProviderId] = @ProviderId
    WHERE
        [Id] = @Id
END
GO

-- Add ReadByProviderId SPROC
IF OBJECT_ID('[dbo].[Transaction_ReadByProviderId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Transaction_ReadByProviderId]
END
GO

CREATE PROCEDURE [dbo].[Transaction_ReadByProviderId]
    @ProviderId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[TransactionView]
    WHERE
        [ProviderId] = @ProviderId
END
GO

-- Refresh modules for SPROCs reliant on 'Transaction' table/view.
IF OBJECT_ID('[dbo].[Transaction_ReadByGatewayId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Transaction_ReadByGatewayId]';
END
GO

IF OBJECT_ID('[dbo].[Transaction_ReadById]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Transaction_ReadById]';
END
GO

IF OBJECT_ID('[dbo].[Transaction_ReadByOrganizationId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Transaction_ReadByOrganizationId]';
END
GO

IF OBJECT_ID('[dbo].[Transaction_ReadByUserId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Transaction_ReadByUserId]';
END
GO

-- Provider Plan

-- Table
IF OBJECT_ID('[dbo].[ProviderPlan]') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProviderPlan] (
        [Id]             UNIQUEIDENTIFIER NOT NULL,
        [ProviderId]     UNIQUEIDENTIFIER NOT NULL,
        [PlanType]       TINYINT          NOT NULL,
        [SeatMinimum]    INT              NULL,
        [PurchasedSeats] INT              NULL,
        [AllocatedSeats] INT              NULL,
        CONSTRAINT [PK_ProviderPlan] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ProviderPlan_Provider] FOREIGN KEY ([ProviderId]) REFERENCES [dbo].[Provider] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [PK_ProviderPlanType] UNIQUE ([ProviderId], [PlanType])
    );
END
GO

-- View
IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'ProviderPlanView')
BEGIN
    DROP VIEW [dbo].[ProviderPlanView]
END
GO

CREATE VIEW [dbo].[ProviderPlanView]
AS
SELECT
    *
FROM
    [dbo].[ProviderPlan]
GO

CREATE PROCEDURE [dbo].[ProviderPlan_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @ProviderId UNIQUEIDENTIFIER,
    @PlanType TINYINT,
    @SeatMinimum INT,
    @PurchasedSeats INT,
    @AllocatedSeats INT
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[ProviderPlan]
    (
        [Id],
        [ProviderId],
        [PlanType],
        [SeatMinimum],
        [PurchasedSeats],
        [AllocatedSeats]
    )
    VALUES
    (
        @Id,
        @ProviderId,
        @PlanType,
        @SeatMinimum,
        @PurchasedSeats,
        @AllocatedSeats
    )
END
GO

-- DeleteById SPROC
IF OBJECT_ID('[dbo].[ProviderPlan_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[ProviderPlan_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[ProviderPlan_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[ProviderPlan]
    WHERE
        [Id] = @Id
END
GO

-- ReadById SPROC
IF OBJECT_ID('[dbo].[ProviderPlan_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[ProviderPlan_ReadById]
END
GO

CREATE PROCEDURE [dbo].[ProviderPlan_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderPlanView]
    WHERE
        [Id] = @Id
END
GO

-- ReadByProviderId SPROC
IF OBJECT_ID('[dbo].[ProviderPlan_ReadByProviderId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[ProviderPlan_ReadByProviderId]
END
GO

CREATE PROCEDURE [dbo].[ProviderPlan_ReadByProviderId]
    @ProviderId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderPlanView]
    WHERE
        [ProviderId] = @ProviderId
END
GO

-- Update SPROC
IF OBJECT_ID('[dbo].[ProviderPlan_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[ProviderPlan_Update]
END
GO

CREATE PROCEDURE [dbo].[ProviderPlan_Update]
    @Id UNIQUEIDENTIFIER,
    @ProviderId UNIQUEIDENTIFIER,
    @PlanType TINYINT,
    @SeatMinimum INT,
    @PurchasedSeats INT,
    @AllocatedSeats INT
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[ProviderPlan]
    SET
        [ProviderId] = @ProviderId,
        [PlanType] = @PlanType,
        [SeatMinimum] = @SeatMinimum,
        [PurchasedSeats] = @PurchasedSeats,
        [AllocatedSeats] = @AllocatedSeats
    WHERE
        [Id] = @Id
END
GO
