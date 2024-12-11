using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationApiKeys.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationApiKeys;

public class GetOrganizationApiKeyQuery : IGetOrganizationApiKeyQuery
{
    private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;

    public GetOrganizationApiKeyQuery(IOrganizationApiKeyRepository organizationApiKeyRepository)
    {
        _organizationApiKeyRepository = organizationApiKeyRepository;
    }

    public async Task<OrganizationApiKey> GetOrganizationApiKeyAsync(
        Guid organizationId,
        OrganizationApiKeyType organizationApiKeyType
    )
    {
        if (!Enum.IsDefined(organizationApiKeyType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(organizationApiKeyType),
                $"Invalid value for enum {nameof(OrganizationApiKeyType)}"
            );
        }

        var apiKeys = await _organizationApiKeyRepository.GetManyByOrganizationIdTypeAsync(
            organizationId,
            organizationApiKeyType
        );

        // NOTE: Currently we only allow one type of api key per organization
        return apiKeys.SingleOrDefault();
    }
}
