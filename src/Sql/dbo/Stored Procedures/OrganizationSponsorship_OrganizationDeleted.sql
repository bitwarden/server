CREATE PROCEDURE [dbo].[OrganizationSponsorship_OrganizationDeleted]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationSponsorship]
    SET
        [SponsoringOrganizationId] = NULL
    WHERE
        [SponsoringOrganizationId] = @OrganizationId AND
        [CloudSponsor] = 0

    UPDATE
        [dbo].[OrganizationSponsorship]
    SET
        [SponsoredOrganizationId] = NULL
    WHERE
        [SponsoredOrganizationId] = @OrganizationId AND
        [CloudSponsor] = 0

    DELETE
    FROM
        [dbo].[OrganizationSponsorship]
    WHERE
        [CloudSponsor] = 1 AND
        ([SponsoredOrganizationId] = @OrganizationId OR
         [SponsoringOrganizationId] = @OrganizationId)
END
GO
