CREATE PROCEDURE [dbo].[Organization_ReadByGatewaySubscriptionId]
    @GatewaySubscriptionId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationView]
    WHERE
        [GatewaySubscriptionId] = @GatewaySubscriptionId
END
