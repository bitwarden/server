using System.Threading.Tasks;
using Bit.Core.Models.Business.Tokenables.Cloud;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Tokens;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise
{
    public class ValidateRedemptionTokenCommand : IValidateRedemptionTokenCommand
    {
        private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
        private readonly IDataProtectorTokenFactory<CloudOrganizationSponsorshipOfferTokenable> _tokenFactory;

        public ValidateRedemptionTokenCommand(IOrganizationSponsorshipRepository organizationSponsorshipRepository,
            IDataProtectorTokenFactory<CloudOrganizationSponsorshipOfferTokenable> tokenFactory)
        {
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
            _tokenFactory = tokenFactory;
        }

        public async Task<bool> ValidateRedemptionTokenAsync(string encryptedToken, string sponsoredUserEmail)
        {

            if (!_tokenFactory.TryUnprotect(encryptedToken, out var tokenable))
            {
                return false;
            }

            var sponsorship = await _organizationSponsorshipRepository.GetByIdAsync(tokenable.Id);
            if (!tokenable.IsValid(sponsorship, sponsoredUserEmail))
            {
                return false;
            }
            return true;
        }
    }
}
