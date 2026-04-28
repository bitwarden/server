CREATE PROCEDURE [dbo].[Provider_ReadAbilityById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderAbilityView]
    WHERE
        [Id] = @Id
END
