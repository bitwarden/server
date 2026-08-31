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

        // A rule that does not gate cannot be bypassed. That covers the rule being absent or another
        // organization's — resource scoping the endpoint has already applied, repeated here so the
        // query is safe to call directly — and, deliberately, the rule being switched off: a disabled
        // rule governs collections that gate nothing, so every collection under it would otherwise be
        // reported as a gap and the warning would be pure noise.
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

        // One pass, and the two lookups are sets on purpose: `Contains` below runs once per mapping,
        // so a list here would turn the whole thing quadratic in the organization's size without any
        // test noticing.
        return collectionCiphers
            .GroupBy(cc => cc.CipherId)
            // Under this rule at all: reachable through at least one collection it governs.
            .Where(g => g.Any(cc => ruleCollectionIds.Contains(cc.CollectionId)))
            // …but not actually gated. The negation of `CipherLeaseGate.IsGated`, and tested against
            // every gating collection in the organization rather than only this rule's: a cipher
            // shared with a collection some OTHER enabled rule governs is still fully gated, and
            // warning about it would send an admin chasing a bypass that does not exist.
            .Where(g => !g.All(cc => gatingCollectionIds.Contains(cc.CollectionId)))
            // The gaps themselves. Taken from the bypassable ciphers only, so a fully gated cipher's
            // collections can never appear here.
            .SelectMany(g => g.Where(cc => !gatingCollectionIds.Contains(cc.CollectionId)))
            .Select(cc => cc.CollectionId)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// The organization's collection ids that gate: those governed by a rule that is currently switched on.
    /// </summary>
    /// <remarks>
    /// Derived from the organization's rules and collections, the same way
    /// <c>CipherLeaseGate.GetLeasingCollectionIdsAsync</c> derives it, and for the same reason: the
    /// organization-scoped collection read returns <c>Collection</c>, which carries the
    /// <c>AccessRuleId</c> association but not the computed <c>HasEnabledAccessRule</c> projection.
    /// </remarks>
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
