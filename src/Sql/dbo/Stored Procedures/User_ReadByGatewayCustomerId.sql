CREATE PROCEDURE [dbo].[User_ReadByGatewayCustomerId]
    @GatewayCustomerId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        [GatewayCustomerId] = @GatewayCustomerId
END
