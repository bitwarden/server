using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SelfHosted
{
    public class GenerateOfferTokenCommand : IGenerateOfferTokenCommand
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly ISymmetricKeyProtectedTokenFactory<SelfHostedOrganizationSponsorshipOfferTokenable> _tokenFactory;
        private readonly ILicensingService _licensingService;

        public GenerateOfferTokenCommand(
            IOrganizationRepository organizationRepository,
            ILicensingService licensingService,
            ISymmetricKeyProtectedTokenFactory<SelfHostedOrganizationSponsorshipOfferTokenable> tokenFactory
        )
        {
            _organizationRepository = organizationRepository;
            _licensingService = licensingService;
            _tokenFactory = tokenFactory;
        }

        public async Task<string> GenerateToken(string key, string sponsoringUserEmail, OrganizationSponsorship sponsorship)
        {
            if (!sponsorship.SponsoringOrganizationId.HasValue)
            {
                throw new BadRequestException("Missing Sponsoring Organization Id, cannot create a cloud offer token");
            }
            var sponsoringOrg = await _organizationRepository.GetByIdAsync(sponsorship.SponsoringOrganizationId.Value);
            var license = _licensingService.ReadOrganizationLicense(sponsoringOrg);
            return _tokenFactory.Protect(key, new SelfHostedOrganizationSponsorshipOfferTokenable(sponsorship, license.Id, sponsoringUserEmail, sponsoringOrg.CloudBillingSyncKey));
        }
    }
}
