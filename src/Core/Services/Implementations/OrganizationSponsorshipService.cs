using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;

namespace Bit.Core.Services
{
    public class OrganizationSponsorshipService : IOrganizationSponsorshipService
    {
        private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;

        public OrganizationSponsorshipService(IOrganizationSponsorshipRepository organizationSponsorshipRepository)
        {
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
        }

        public async Task OfferSponsorshipAsync(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser, string sponsoredEmail)
        {
            // TODO: send sponsorship email, update sponsorship with offered email
            throw new NotImplementedException();
        }

        public async Task SetUpSponsorshipAsync(OrganizationSponsorship sponsorship, Organization sponsoredOrganization)
        {
            // TODO: set up sponsorship
            throw new NotImplementedException();
        }

        public async Task RemoveSponsorshipAsync(OrganizationSponsorship sponsorship)
        {
            // TODO: remove sponsorship
            throw new NotImplementedException();
        }

    }
}
