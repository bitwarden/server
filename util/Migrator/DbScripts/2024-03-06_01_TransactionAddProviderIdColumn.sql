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
    @ProviderId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Amount MONEY,
    @Refunded BIT,
    @RefundedAmount MONEY,
    @Details NVARCHAR(100),
    @PaymentMethodType TINYINT,
    @Gateway TINYINT,
    @GatewayId VARCHAR(50),
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Transaction]
    (
        [Id],
        [UserId],
        [OrganizationId],
        [ProviderId],
        [Type],
        [Amount],
        [Refunded],
        [RefundedAmount],
        [Details],
        [PaymentMethodType],
        [Gateway],
        [GatewayId],
        [CreationDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @OrganizationId,
        @ProviderId,
        @Type,
        @Amount,
        @Refunded,
        @RefundedAmount,
        @Details,
        @PaymentMethodType,
        @Gateway,
        @GatewayId,
        @CreationDate
    )
END
GO

-- Alter 'Transaction_Update' SPROC to add 'ProviderId' column.
CREATE OR ALTER PROCEDURE [dbo].[Transaction_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ProviderId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Amount MONEY,
    @Refunded BIT,
    @RefundedAmount MONEY,
    @Details NVARCHAR(100),
    @PaymentMethodType TINYINT,
    @Gateway TINYINT,
    @GatewayId VARCHAR(50),
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Transaction]
    SET
        [UserId] = @UserId,
        [OrganizationId] = @OrganizationId,
        [ProviderId] = @ProviderId,
        [Type] = @Type,
        [Amount] = @Amount,
        [Refunded] = @Refunded,
        [RefundedAmount] = @RefundedAmount,
        [Details] = @Details,
        [PaymentMethodType] = @PaymentMethodType,
        [Gateway] = @Gateway,
        [GatewayId] = @GatewayId,
        [CreationDate] = @CreationDate
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