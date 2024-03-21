CREATE PROCEDURE [dbo].[ProviderPlan_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[ProviderPlan]
    WHERE
        [Id] = @Id
END
