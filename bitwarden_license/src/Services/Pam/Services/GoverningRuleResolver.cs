using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Models;
using Bit.Services.Pam.Models.Conditions;

namespace Bit.Services.Pam.Services;

public class GoverningRuleResolver : IGoverningRuleResolver
{
    private readonly ICollectionCipherRepository _collectionCipherRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IAccessRuleRepository _accessRuleRepository;

    public GoverningRuleResolver(
        ICollectionCipherRepository collectionCipherRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        _collectionCipherRepository = collectionCipherRepository;
        _collectionRepository = collectionRepository;
        _accessRuleRepository = accessRuleRepository;
    }

    public async Task<GoverningRule?> ResolveAsync(Guid userId, Guid cipherId, AccessSignals signals)
    {
        var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdCipherIdAsync(userId, cipherId);
        if (collectionCiphers.Count == 0)
        {
            return null;
        }

        var collectionIds = collectionCiphers.Select(cc => cc.CollectionId).ToHashSet();
        var collections = await _collectionRepository.GetManyByManyIdsAsync(collectionIds);

        var paths = collections.Where(c => collectionIds.Contains(c.Id)).ToList();

        // Every path to the cipher must gate. A path with no rule, a disabled rule, or a deleted one is an escape,
        // so nothing governs.
        var candidates = new List<(Collection Collection, AccessRule Rule)>();
        foreach (var collection in paths)
        {
            if (!collection.AccessRuleId.HasValue)
            {
                return null;
            }

            var accessRule = await _accessRuleRepository.GetByIdAsync(collection.AccessRuleId.Value);
            if (accessRule is not { Enabled: true })
            {
                return null;
            }

            candidates.Add((collection, accessRule));
        }

        // Only reachable when no mapped collection loaded: no path is left to gate.
        if (candidates.Count == 0)
        {
            return null;
        }

        // Oldest wins, even where a newer path's rule would be more permissive.
        var (governingCollection, governingRule) = candidates
            .OrderBy(c => c.Rule.CreationDate)
            .ThenBy(c => c.Rule.Id)
            .First();

        return Build(governingCollection.OrganizationId, governingCollection.Id, governingRule);
    }

    public async Task<GoverningRule?> ResolvePinnedAsync(Guid ruleId, Guid collectionId)
    {
        var rule = await _accessRuleRepository.GetByIdAsync(ruleId);

        // A disabled or deleted rule governs nothing, as in ResolveAsync.
        return rule is { Enabled: true } ? Build(rule.OrganizationId, collectionId, rule) : null;
    }

    private static GoverningRule Build(Guid organizationId, Guid collectionId, AccessRule rule)
    {
        var (conditions, unreadable) = Parse(rule.Conditions);

        // Read from the conditions, not from evaluating them: Combine gives Deny precedence and would hide the gate.
        var requiresHumanApproval = conditions.Any(c => c is HumanApprovalCondition);

        return new GoverningRule(
            organizationId,
            collectionId,
            requiresHumanApproval,
            conditions)
        {
            RuleId = rule.Id,
            AllowsExtensions = rule.AllowsExtensions,
            MaxExtensionDurationSeconds = rule.MaxExtensionDurationSeconds,
            DefaultLeaseDurationSeconds = rule.DefaultLeaseDurationSeconds,
            MaxLeaseDurationSeconds = rule.MaxLeaseDurationSeconds,
            ConditionsUnreadable = unreadable,
        };
    }

    /// <summary>
    /// Parses the stored conditions JSON. A malformed document fails safe to a single human-approval condition,
    /// flagged as unreadable.
    /// </summary>
    private static (IReadOnlyList<AccessCondition> Conditions, bool Unreadable) Parse(string conditionsJson)
    {
        try
        {
            var conditions = JsonSerializer.Deserialize<List<AccessCondition>>(conditionsJson, AccessConditionJson.Options);
            return conditions is null ? FailSafe() : (conditions, false);
        }
        // The polymorphic reader reports a missing or unknown "kind" as NotSupportedException. Caught too, so an
        // uninterpretable document routes to an approver instead of throwing.
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return FailSafe();
        }
    }

    private static (IReadOnlyList<AccessCondition> Conditions, bool Unreadable) FailSafe() =>
        ([new HumanApprovalCondition()], true);
}
