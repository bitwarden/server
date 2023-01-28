CREATE PROCEDURE [dbo].[OrganizationSponsorship_ReadLatestBySponsoringOrganizationId]
    @SponsoringOrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SELECT TOP 1
        [LastSyncDate]
    FROM
        [dbo].[OrganizationSponsorshipView]
    WHERE
        [SponsoringOrganizationId] = @SponsoringOrganizationId AND
        [LastSyncDate] IS NOT NULL
    ORDER BY [LastSyncDate] DESC
END
