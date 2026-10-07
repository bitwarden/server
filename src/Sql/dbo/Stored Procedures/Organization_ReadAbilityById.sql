CREATE PROCEDURE [dbo].[Organization_ReadAbilityById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationAbilityView]
    WHERE
        [Id] = @Id
END
