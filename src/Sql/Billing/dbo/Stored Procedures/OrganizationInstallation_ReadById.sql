CREATE PROCEDURE [dbo].[OrganizationInstallation_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationInstallationView]
    WHERE
        [Id] = @Id
END
