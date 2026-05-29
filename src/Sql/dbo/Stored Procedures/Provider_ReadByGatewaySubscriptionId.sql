CREATE PROCEDURE [dbo].[Provider_ReadByGatewaySubscriptionId]
    @GatewaySubscriptionId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderView]
    WHERE
        [GatewaySubscriptionId] = @GatewaySubscriptionId
END
