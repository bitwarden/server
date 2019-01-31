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