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
        [SponsoringOrganizationId] = @OrganizationId

    UPDATE
        [dbo].[OrganizationSponsorship]
    SET
        [SponsoredOrganizationId] = NULL
    WHERE
        [SponsoredOrganizationId] = @OrganizationId
END
GO
