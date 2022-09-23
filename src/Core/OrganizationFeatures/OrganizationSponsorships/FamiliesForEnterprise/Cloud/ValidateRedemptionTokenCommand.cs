using Bit.Core.Entities;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Tokens;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;

public class ValidateRedemptionTokenCommand : IValidateRedemptionTokenCommand
{
    private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
    private readonly IDataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable> _dataProtectorTokenFactory;

    public ValidateRedemptionTokenCommand(IOrganizationSponsorshipRepository organizationSponsorshipRepository,
        IDataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable> dataProtectorTokenFactory)
    {
        _organizationSponsorshipRepository = organizationSponsorshipRepository;
        _dataProtectorTokenFactory = dataProtectorTokenFactory;
    }

    public async Task<(bool valid, OrganizationSponsorship sponsorship)> ValidateRedemptionTokenAsync(string encryptedToken, string sponsoredUserEmail)
    {

        if (!_dataProtectorTokenFactory.TryUnprotect(encryptedToken, out var tokenable))
        {
            return (false, null);
        }

        var sponsorship = await _organizationSponsorshipRepository.GetByIdAsync(tokenable.Id);
        if (!tokenable.IsValid(sponsorship, sponsoredUserEmail))
        {
            return (false, sponsorship);
        }
        return (true, sponsorship);
    }
}
