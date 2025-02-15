CREATE PROCEDURE [dbo].[OrganizationInstallation_ReadByInstallationId]
    @InstallationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationInstallationView]
    WHERE
        [InstallationId] = @InstallationId
END
