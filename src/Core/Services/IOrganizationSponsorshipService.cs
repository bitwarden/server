using System;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Services
{
    public interface IOrganizationSponsorshipService
    {
        Task<(bool valid, OrganizationSponsorship sponsorship)> ValidateRedemptionTokenAsync(string encryptedToken, string currentUserEmail);
        Task OfferSponsorshipAsync(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            PlanSponsorshipType sponsorshipType, string sponsoredEmail, string friendlyName, string sponsoringUserEmail);
        Task ResendSponsorshipOfferAsync(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            OrganizationSponsorship sponsorship, string sponsoringUserEmail);
        Task SendSponsorshipOfferAsync(OrganizationSponsorship sponsorship, string sponsoringOrgUserEmail);
        Task SetUpSponsorshipAsync(OrganizationSponsorship sponsorship,
            Organization sponsoredOrganization);
        Task<bool> ValidateSponsorshipAsync(Guid sponsoredOrganizationId);
        Task RevokeSponsorshipAsync(Organization sponsoredOrganization, OrganizationSponsorship sponsorship);
        Task RemoveSponsorshipAsync(Organization sponsoredOrganization, OrganizationSponsorship sponsorship);
    }
}
