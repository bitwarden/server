using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces
{
    public interface ISendSponsorshipOfferCommand
    {
        Task BulkSendSponsorshipOfferAsync(Organization sponsoringOrg, IEnumerable<OrganizationSponsorship> invites);
        Task SendSponsorshipOfferAsync(OrganizationSponsorship sponsorship, Organization sponsoringOrg);
        Task SendSponsorshipOfferAsync(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            OrganizationSponsorship sponsorship);
    }
}
