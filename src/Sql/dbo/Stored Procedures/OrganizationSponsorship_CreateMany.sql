CREATE PROCEDURE [dbo].[OrganizationSponsorship_CreateMany]
    @OrganizationSponsorshipsInput [dbo].[OrganizationSponsorshipType] READONLY
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationSponsorship]
    (
		[Id],
        [SponsoringOrganizationId],
        [SponsoringOrganizationUserID],
        [SponsoredOrganizationId],
        [FriendlyName],
        [OfferedToEmail],
        [PlanSponsorshipType],
        [ToDelete],
        [LastSyncDate],
        [ValidUntil]
    )
    SELECT
        OS.[Id],
        OS.[SponsoringOrganizationId],
        OS.[SponsoringOrganizationUserID],
        OS.[SponsoredOrganizationId],
        OS.[FriendlyName],
        OS.[OfferedToEmail],
        OS.[PlanSponsorshipType],
        OS.[ToDelete],
        OS.[LastSyncDate],
        OS.[ValidUntil]
    FROM
        @OrganizationSponsorshipsInput OS
END