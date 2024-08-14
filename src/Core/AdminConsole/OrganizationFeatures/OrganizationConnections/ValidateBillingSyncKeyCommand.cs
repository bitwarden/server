using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationConnections.Interfaces;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationConnections;

public class ValidateBillingSyncKeyCommand : IValidateBillingSyncKeyCommand
{
    private readonly IOrganizationApiKeyRepository _apiKeyRepository;

    public ValidateBillingSyncKeyCommand(
        IOrganizationApiKeyRepository organizationApiKeyRepository)
    {
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

        var orgApiKey = (await _apiKeyRepository.GetManyByOrganizationIdTypeAsync(organization.Id, Core.Enums.OrganizationApiKeyType.BillingSync)).FirstOrDefault();
        if (string.Equals(orgApiKey.ApiKey, billingSyncKey))
        {
            return true;
        }
        return false;
    }
}
