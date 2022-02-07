using Bit.Core.Entities;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Tokens;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SelfHosted
{
    public class GenerateOfferTokenCommand : IGenerateOfferTokenCommand
    {
        private readonly ISymmetricKeyProtectedTokenFactory<OrganizationSponsorshipOfferTokenable> _tokenFactory;

        public GenerateOfferTokenCommand(
            ISymmetricKeyProtectedTokenFactory<OrganizationSponsorshipOfferTokenable> tokenFactory
        )
        {
            _tokenFactory = tokenFactory;
        }
        public string GenerateToken(string key, string sponsoringUserEmail, OrganizationSponsorship sponsorship)
        {
            return _tokenFactory.Protect(key, new OrganizationSponsorshipOfferTokenable(sponsorship)
            {
                SponsoringUserEmail = sponsoringUserEmail
            });
        }
    }
}
