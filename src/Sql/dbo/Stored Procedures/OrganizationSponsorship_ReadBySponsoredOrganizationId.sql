CREATE PROCEDURE [dbo].[OrganizationSponsorship_ReadBySponsoredOrganizationId]
    @SponsoredOrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationSponsorshipView]
    WHERE
        [SponsoredOrganizationId] = @SponsoredOrganizationId
END
GO
