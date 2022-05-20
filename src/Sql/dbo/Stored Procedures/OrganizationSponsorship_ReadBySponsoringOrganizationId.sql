CREATE PROCEDURE [dbo].[OrganizationSponsorship_ReadBySponsoringOrganizationId]
    @SponsoringOrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
    	*
    FROM
        [dbo].[OrganizationSponsorshipView]
    WHERE
        [SponsoringOrganizationId] = @SponsoringOrganizationId
END