using Bit.Core.AdminConsole.Entities;
using Bit.Core.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace Bit.Core.Services;

public class OrganizationCache(
    IMemoryCache memoryCache,
    TimeSpan cacheEntryTtl,
    IOrganizationRepository organizationRepository) :
    IntegrationTemplatePropertyCache<Guid, Organization>(
        memoryCache,
        cacheEntryTtl),
    IOrganizationCache
{
    protected override Task<Organization?> LoadValueFromRepositoryAsync(Guid key)
    {
        return organizationRepository.GetByIdAsync(key);
    }
}
