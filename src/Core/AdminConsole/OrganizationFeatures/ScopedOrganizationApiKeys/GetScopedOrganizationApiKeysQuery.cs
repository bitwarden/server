using Bit.Core.AdminConsole.OrganizationFeatures.ScopedOrganizationApiKeys.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.ScopedOrganizationApiKeys;

public class GetScopedOrganizationApiKeysQuery(
    IApiKeyRepository apiKeyRepository)
    : IGetScopedOrganizationApiKeysQuery
{
    public Task<ICollection<ApiKey>> GetManyByOrganizationIdAsync(Guid organizationId) =>
        apiKeyRepository.GetManyByOrganizationIdAsync(organizationId);
}
