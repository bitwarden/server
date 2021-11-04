using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public interface IOrganizationSponsorshipService
    {
        Task<bool> ValidateRedemptionTokenAsync(string encryptedToken);
        Task OfferSponsorshipAsync(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser, string sponsoredEmail);
        Task SetUpSponsorshipAsync(OrganizationSponsorship sponsorship, Organization sponsoredOrganization);
        Task RemoveSponsorshipAsync(OrganizationSponsorship sponsorship);
    }
}
