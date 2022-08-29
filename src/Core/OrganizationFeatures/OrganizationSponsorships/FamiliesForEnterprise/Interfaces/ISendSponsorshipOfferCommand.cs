using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;

public interface ISendSponsorshipOfferCommand
{
    Task BulkSendSponsorshipOfferAsync(string sponsoringOrgName, IEnumerable<OrganizationSponsorship> invites);
    Task SendSponsorshipOfferAsync(OrganizationSponsorship sponsorship, string sponsoringOrgName);
    Task SendSponsorshipOfferAsync(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
        OrganizationSponsorship sponsorship);
}
