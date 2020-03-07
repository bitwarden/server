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