using Bit.Core.Entities;
using Bit.Core.OrganizationFeatures.OrganizationApiKeys.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationApiKeys;

public class RotateOrganizationApiKeyCommand : IRotateOrganizationApiKeyCommand
{
    private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;

    public RotateOrganizationApiKeyCommand(IOrganizationApiKeyRepository organizationApiKeyRepository)
    {
        _organizationApiKeyRepository = organizationApiKeyRepository;
    }

    public async Task<OrganizationApiKey> RotateApiKeyAsync(OrganizationApiKey organizationApiKey)
    {
        organizationApiKey.ApiKey = CoreHelpers.SecureRandomString(30);
        organizationApiKey.RevisionDate = DateTime.UtcNow;
        await _organizationApiKeyRepository.UpsertAsync(organizationApiKey);
        return organizationApiKey;
    }
}
