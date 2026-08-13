CREATE PROCEDURE [dbo].[User_ReadByGatewaySubscriptionId]
    @GatewaySubscriptionId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        [GatewaySubscriptionId] = @GatewaySubscriptionId
END
