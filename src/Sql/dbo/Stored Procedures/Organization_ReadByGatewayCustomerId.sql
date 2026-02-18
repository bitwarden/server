CREATE PROCEDURE [dbo].[Organization_ReadByGatewayCustomerId]
    @GatewayCustomerId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationView]
    WHERE
        [GatewayCustomerId] = @GatewayCustomerId
END
