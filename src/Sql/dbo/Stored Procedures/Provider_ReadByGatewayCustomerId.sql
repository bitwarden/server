CREATE PROCEDURE [dbo].[Provider_ReadByGatewayCustomerId]
    @GatewayCustomerId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderView]
    WHERE
        [GatewayCustomerId] = @GatewayCustomerId
END
