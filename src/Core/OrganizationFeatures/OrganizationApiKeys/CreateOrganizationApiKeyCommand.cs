using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.OrganizationFeatures.OrganizationApiKeys.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationApiKeys;

public class CreateOrganizationApiKeyCommand : ICreateOrganizationApiKeyCommand
{
    private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;

    public CreateOrganizationApiKeyCommand(IOrganizationApiKeyRepository organizationApiKeyRepository)
    {
        _organizationApiKeyRepository = organizationApiKeyRepository;
    }

    public async Task<OrganizationApiKey> CreateAsync(Guid organizationId,
        OrganizationApiKeyType organizationApiKeyType)
    {
        var apiKey = new OrganizationApiKey
        {
            OrganizationId = organizationId,
            Type = organizationApiKeyType,
            ApiKey = CoreHelpers.SecureRandomString(30),
            RevisionDate = DateTime.UtcNow,
        };

        await _organizationApiKeyRepository.CreateAsync(apiKey);
        return apiKey;
    }
}
