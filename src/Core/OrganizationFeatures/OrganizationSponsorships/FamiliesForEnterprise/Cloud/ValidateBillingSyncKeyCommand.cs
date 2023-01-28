using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;

public class ValidateBillingSyncKeyCommand : IValidateBillingSyncKeyCommand
{
    private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
    private readonly IOrganizationApiKeyRepository _apiKeyRepository;

    public ValidateBillingSyncKeyCommand(
        IOrganizationSponsorshipRepository organizationSponsorshipRepository,
        IOrganizationApiKeyRepository organizationApiKeyRepository)
    {
        _organizationSponsorshipRepository = organizationSponsorshipRepository;
        _apiKeyRepository = organizationApiKeyRepository;
    }

    public async Task<bool> ValidateBillingSyncKeyAsync(Organization organization, string billingSyncKey)
    {
        if (organization == null)
        {
            throw new BadRequestException("Invalid organization");
        }
        if (string.IsNullOrWhiteSpace(billingSyncKey))
        {
            return false;
        }

        var orgApiKey = (await _apiKeyRepository.GetManyByOrganizationIdTypeAsync(organization.Id, Enums.OrganizationApiKeyType.BillingSync)).FirstOrDefault();
        if (string.Equals(orgApiKey.ApiKey, billingSyncKey))
        {
            return true;
        }
        return false;
    }
}
