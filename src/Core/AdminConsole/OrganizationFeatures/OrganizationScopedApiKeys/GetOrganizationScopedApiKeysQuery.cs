using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationScopedApiKeys.Interfaces;
using Bit.Core.AdminConsole.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationScopedApiKeys;

public class GetOrganizationScopedApiKeysQuery(
    IOrganizationScopedApiKeyRepository organizationScopedApiKeyRepository)
    : IGetOrganizationScopedApiKeysQuery
{
    public Task<ICollection<OrganizationScopedApiKey>> GetManyByOrganizationIdAsync(Guid organizationId) =>
        organizationScopedApiKeyRepository.GetManyByOrganizationIdAsync(organizationId);
}
