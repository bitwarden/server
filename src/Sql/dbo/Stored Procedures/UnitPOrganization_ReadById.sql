CREATE PROCEDURE [dbo].[UnitPOrganization_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UnitPOrganizationView]
    WHERE
        [Id] = @Id
END
