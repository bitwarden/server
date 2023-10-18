﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationApiKeys.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationApiKeys;

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
