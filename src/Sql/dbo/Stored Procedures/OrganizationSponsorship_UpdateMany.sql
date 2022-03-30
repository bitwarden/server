CREATE PROCEDURE [dbo].[OrganizationSponsorship_UpdateMany]
    @OrganizationSponsorshipsInput [dbo].[OrganizationSponsorshipType] READONLY
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        OS
    SET 
        [Id] = OSI.[Id],
        [SponsoringOrganizationId] = OSI.[SponsoringOrganizationId],
        [SponsoringOrganizationUserID] = OSI.[SponsoringOrganizationUserID],
        [SponsoredOrganizationId] = OSI.[SponsoredOrganizationId],
        [FriendlyName] = OSI.[FriendlyName],
        [OfferedToEmail] = OSI.[OfferedToEmail],
        [PlanSponsorshipType] = OSI.[PlanSponsorshipType],
        [ToDelete] = OSI.[ToDelete],
        [LastSyncDate] = OSI.[LastSyncDate],
        [ValidUntil] = OSI.[ValidUntil]
    FROM
        [dbo].[OrganizationSponsorship] OS
    INNER JOIN
        @OrganizationSponsorshipsInput OSI ON OS.Id = OSI.Id

END