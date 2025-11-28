CREATE PROCEDURE [dbo].[ProviderPlan_ReadByProviderId]
    @ProviderId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderPlanView]
    WHERE
        [ProviderId] = @ProviderId
END
