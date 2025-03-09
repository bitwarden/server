CREATE PROCEDURE [dbo].[OrganizationInstallation_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationInstallationView]
    WHERE
        [OrganizationId] = @OrganizationId
END
