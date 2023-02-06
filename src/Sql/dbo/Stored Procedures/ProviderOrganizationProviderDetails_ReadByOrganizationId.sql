CREATE PROCEDURE [dbo].[ProviderOrganizationProviderDetails_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderOrganizationProviderDetailsView]
    WHERE
        [OrganizationId] = @OrganizationId
END
