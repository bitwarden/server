CREATE PROCEDURE [dbo].[ProviderOrganizationOrganizationDetails_ReadByProviderId]
    @ProviderId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderOrganizationOrganizationDetailsView]
    WHERE
        [ProviderId] = @ProviderId
END
