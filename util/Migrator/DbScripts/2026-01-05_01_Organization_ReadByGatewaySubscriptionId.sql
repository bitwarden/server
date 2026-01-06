CREATE OR ALTER PROCEDURE [dbo].[Organization_ReadByGatewaySubscriptionId]
    @GatewaySubscriptionId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        *
    FROM
        [dbo].[OrganizationView]
    WHERE
        [GatewaySubscriptionId] = @GatewaySubscriptionId
END
GO
