using System.Threading.Tasks;
using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces
{
    public interface IResendSponsorshipOfferCommand
    {
        Task ResendSponsorshipOfferAsync(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            OrganizationSponsorship sponsorship, string sponsoringUserEmail);
    }
}
