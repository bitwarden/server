using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace Bit.Core.Services;

public class GroupCache(
    IMemoryCache memoryCache,
    TimeSpan cacheEntryTtl,
    IGroupRepository groupRepository) :
    IntegrationTemplatePropertyCache<Guid, Group>(
        memoryCache,
        cacheEntryTtl),
    IGroupCache
{
    protected override Task<Group?> LoadValueFromRepositoryAsync(Guid key)
    {
        return groupRepository.GetByIdAsync(key);
    }
}
