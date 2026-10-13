using Bit.Core.Repositories;
using Bit.Pam.Repositories;

namespace Bit.Services.Pam.Services;

public class GatingCollectionResolver : IGatingCollectionResolver
{
    private readonly IAccessRuleRepository _accessRuleRepository;
    private readonly ICollectionRepository _collectionRepository;

    public GatingCollectionResolver(
        IAccessRuleRepository accessRuleRepository,
        ICollectionRepository collectionRepository)
    {
        _accessRuleRepository = accessRuleRepository;
        _collectionRepository = collectionRepository;
    }

    public async Task<ISet<Guid>> GetGatingCollectionIdsAsync(Guid organizationId)
    {
        var enabledRuleIds = (await _accessRuleRepository.GetManyByOrganizationIdAsync(organizationId))
            .Where(r => r.Enabled)
            .Select(r => r.Id)
            .ToHashSet();
        if (enabledRuleIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var collections = await _collectionRepository.GetManyByOrganizationIdAsync(organizationId);
        return collections
            .Where(c => c.AccessRuleId.HasValue && enabledRuleIds.Contains(c.AccessRuleId.Value))
            .Select(c => c.Id)
            .ToHashSet();
    }
}
