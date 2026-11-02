using Bit.Core.Repositories;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.OrganizationFeatures.Queries;

/// <inheritdoc cref="IListRuleBypassableCiphersQuery"/>
public class ListRuleBypassableCiphersQuery : IListRuleBypassableCiphersQuery
{
    private readonly IAccessRuleRepository _accessRuleRepository;
    private readonly IGatingCollectionResolver _gatingCollectionResolver;
    private readonly ICollectionCipherRepository _collectionCipherRepository;

    public ListRuleBypassableCiphersQuery(
        IAccessRuleRepository accessRuleRepository,
        IGatingCollectionResolver gatingCollectionResolver,
        ICollectionCipherRepository collectionCipherRepository)
    {
        _accessRuleRepository = accessRuleRepository;
        _gatingCollectionResolver = gatingCollectionResolver;
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

        var gatingCollectionIds = await _gatingCollectionResolver.GetGatingCollectionIdsAsync(organizationId);
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
}
