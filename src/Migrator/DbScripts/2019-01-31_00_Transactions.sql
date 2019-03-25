IF OBJECT_ID('[dbo].[Transaction]') IS NULL
BEGIN
    CREATE TABLE [dbo].[Transaction] (
        [Id]                    UNIQUEIDENTIFIER    NOT NULL,
        [UserId]                UNIQUEIDENTIFIER    NULL,
        [OrganizationId]        UNIQUEIDENTIFIER    NULL,
        [Type]                  TINYINT             NOT NULL,
        [Amount]                MONEY               NOT NULL,
        [Refunded]              BIT                 NULL,
        [RefundedAmount]        MONEY               NULL,
        [Details]               NVARCHAR(100)       NULL,
        [PaymentMethodType]     TINYINT             NULL,
        [Gateway]               TINYINT             NULL,
        [GatewayId]             VARCHAR(50)         NULL,
        [CreationDate]          DATETIME2 (7)       NOT NULL,
        CONSTRAINT [PK_Transaction] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Transaction_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Transaction_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_Transaction_Gateway_GatewayId]
        ON [dbo].[Transaction]([Gateway] ASC, [GatewayId] ASC)
        WHERE [Gateway] IS NOT NULL AND [GatewayId] IS NOT NULL;

    CREATE NONCLUSTERED INDEX [IX_Transaction_UserId_OrganizationId_CreationDate]
        ON [dbo].[Transaction]([UserId] ASC, [OrganizationId] ASC, [CreationDate] ASC);
END
GO

IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'TransactionView')
BEGIN
    DROP VIEW [dbo].[TransactionView]
END
GO

CREATE VIEW [dbo].[TransactionView]
AS
SELECT
    *
FROM
    [dbo].[Transaction]
GO

IF OBJECT_ID('[dbo].[Transaction_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Transaction_Create]
END
GO

CREATE PROCEDURE [dbo].[Transaction_Create]
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
    @CreationDate DATETIME2(7)
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
        [CreationDate]
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
        @CreationDate
    )
END
GO

IF OBJECT_ID('[dbo].[Transaction_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Transaction_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[Transaction_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[Transaction]
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[Transaction_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Transaction_ReadById]
END
GO

CREATE PROCEDURE [dbo].[Transaction_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[TransactionView]
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[Transaction_ReadByOrganizationId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Transaction_ReadByOrganizationId]
END
GO

CREATE PROCEDURE [dbo].[Transaction_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[TransactionView]
    WHERE
        [UserId] IS NULL
        AND [OrganizationId] = @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[Transaction_ReadByUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Transaction_ReadByUserId]
END
GO

CREATE PROCEDURE [dbo].[Transaction_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[TransactionView]
    WHERE
        [UserId] = @UserId
END
GO

IF OBJECT_ID('[dbo].[Transaction_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Transaction_Update]
END
GO

CREATE PROCEDURE [dbo].[Transaction_Update]
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
    @CreationDate DATETIME2(7)
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
        [CreationDate] = @CreationDate
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[Transaction_ReadByGatewayId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Transaction_ReadByGatewayId]
END
GO

CREATE PROCEDURE [dbo].[Transaction_ReadByGatewayId]
    @Gateway TINYINT,
    @GatewayId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[TransactionView]
    WHERE
        [Gateway] = @Gateway
        AND [GatewayId] = @GatewayId
END
GO

IF OBJECT_ID('[dbo].[User_ReadByPremiumRenewal]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_ReadByPremiumRenewal]
END
GO
