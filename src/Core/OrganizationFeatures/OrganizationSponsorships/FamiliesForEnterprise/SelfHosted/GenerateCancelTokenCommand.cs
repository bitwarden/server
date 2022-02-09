using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business.Tokenables.SelfHosted;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SelfHosted
{
    public class GenerateCancelTokenCommand : IGenerateCancelTokenCommand
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly ISymmetricKeyProtectedTokenFactory<SelfHostedOrganizationSponsorshipCancelTokenable> _tokenFactory;
        private readonly ILicensingService _licensingService;

        public GenerateCancelTokenCommand(
            IOrganizationRepository organizationRepository,
            ILicensingService licensingService,
            ISymmetricKeyProtectedTokenFactory<SelfHostedOrganizationSponsorshipCancelTokenable> tokenFactory
        )
        {
            _organizationRepository = organizationRepository;
            _licensingService = licensingService;
            _tokenFactory = tokenFactory;
        }

        public async Task<string> GenerateToken(string key, OrganizationSponsorship sponsorship)
        {
            if (!sponsorship.SponsoringOrganizationId.HasValue)
            {
                throw new BadRequestException("Missing Sponsoring Organization Id, cannot create a cloud offer token");
            }
            var sponsoringOrg = await _organizationRepository.GetByIdAsync(sponsorship.SponsoringOrganizationId.Value);
            var license = _licensingService.ReadOrganizationLicense(sponsoringOrg);
            return _tokenFactory.Protect(key, new SelfHostedOrganizationSponsorshipCancelTokenable(sponsorship, license.Id, sponsoringOrg.CloudBillingSyncKey));
        }
    }
}
