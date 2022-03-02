CREATE PROCEDURE [dbo].[OrganizationSponsorship_ReadFirstBySponsoringOrganizationId]
    @SponsoringOrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SELECT TOP 1
        *
    FROM
        [dbo].[OrganizationSponsorshipView]
    WHERE
        [SponsoringOrganizationId] = @SponsoringOrganizationId AND
        [LastSyncDate] IS NOT NULL
END
