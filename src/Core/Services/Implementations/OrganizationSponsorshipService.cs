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
    }
}
