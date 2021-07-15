CREATE PROCEDURE [dbo].[ProviderOrganization_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderOrganizationView]
    WHERE
        [OrganizationId] = @OrganizationId
END
