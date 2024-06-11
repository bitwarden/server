CREATE PROCEDURE [dbo].[ProviderPlan_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderPlanView]
    WHERE
        [Id] = @Id
END
