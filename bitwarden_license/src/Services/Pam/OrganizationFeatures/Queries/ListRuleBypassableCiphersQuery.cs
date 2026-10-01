using Bit.Core.Repositories;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

namespace Bit.Services.Pam.OrganizationFeatures.Queries;

/// <inheritdoc cref="IListRuleBypassableCiphersQuery"/>
public class ListRuleBypassableCiphersQuery : IListRuleBypassableCiphersQuery
{
    private readonly IAccessRuleRepository _accessRuleRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly ICollectionCipherRepository _collectionCipherRepository;

    public ListRuleBypassableCiphersQuery(
        IAccessRuleRepository accessRuleRepository,
        ICollectionRepository collectionRepository,
        ICollectionCipherRepository collectionCipherRepository)
    {
        _accessRuleRepository = accessRuleRepository;
        _collectionRepository = collectionRepository;
        _collectionCipherRepository = collectionCipherRepository;
    }

    public async Task<ICollection<Guid>> GetUngatedCollectionIdsAsync(Guid organizationId, Guid ruleId)
    {
        var rule = await _accessRuleRepository.GetDetailsByIdAsync(ruleId);

        // A rule that does not gate cannot be bypassed: absent, another organization's, or disabled.
        if (rule is null || rule.OrganizationId != organizationId || !rule.Enabled)
        {
            return [];
        }

        var ruleCollectionIds = rule.CollectionIds.ToHashSet();
        if (ruleCollectionIds.Count == 0)
        {
            return [];
        }

        var gatingCollectionIds = await GetGatingCollectionIdsAsync(organizationId);
        var collectionCiphers = await _collectionCipherRepository.GetManyByOrganizationIdAsync(organizationId);

        return collectionCiphers
            .GroupBy(cc => cc.CipherId)
            // Reachable through at least one collection the rule governs…
            .Where(g => g.Any(cc => ruleCollectionIds.Contains(cc.CollectionId)))
            // …but not gated, judged against every gating collection in the organization.
            .Where(g => !g.All(cc => gatingCollectionIds.Contains(cc.CollectionId)))
            .SelectMany(g => g.Where(cc => !gatingCollectionIds.Contains(cc.CollectionId)))
            .Select(cc => cc.CollectionId)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// The organization's collection ids gated by an enabled rule.
    /// </summary>
    private async Task<ISet<Guid>> GetGatingCollectionIdsAsync(Guid organizationId)
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
