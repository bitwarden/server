using System.Text.Json;
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
    private readonly IAccessRuleEngine _ruleEngine;

    public GoverningRuleResolver(
        ICollectionCipherRepository collectionCipherRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository,
        IAccessRuleEngine ruleEngine)
    {
        _collectionCipherRepository = collectionCipherRepository;
        _collectionRepository = collectionRepository;
        _accessRuleRepository = accessRuleRepository;
        _ruleEngine = ruleEngine;
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
        var candidates = new List<(AccessRule Rule, GoverningRule Governing)>();
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

            candidates.Add((accessRule, Build(collection.OrganizationId, collection.Id, accessRule)));
        }

        // Only reachable when no mapped collection loaded: no path is left to gate.
        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates
            .OrderBy(c => TierOf(c.Governing, signals))
            .ThenBy(c => c.Rule.CreationDate)
            .ThenBy(c => c.Rule.Id)
            .First()
            .Governing;
    }

    public async Task<GoverningRule?> ResolvePinnedAsync(Guid ruleId, Guid collectionId)
    {
        var rule = await _accessRuleRepository.GetByIdAsync(ruleId);

        // A disabled or deleted rule governs nothing, as in ResolveAsync.
        return rule is { Enabled: true } ? Build(rule.OrganizationId, collectionId, rule) : null;
    }

    /// <summary>Where a governing candidate ranks; a lower tier governs.</summary>
    private enum Tier
    {
        AutomaticAllows,
        HumanApproval,
        // Still governs, so Submit surfaces the denial.
        AutomaticDenies,
        // Its fail-safe approval cannot activate while the rule stays unreadable, so any readable rule goes first.
        Unreadable,
    }

    // Predicts what Submit and Activate do with the chosen rule; change them together.
    private Tier TierOf(GoverningRule rule, AccessSignals signals)
    {
        if (rule.ConditionsUnreadable)
        {
            return Tier.Unreadable;
        }

        if (rule.RequiresHumanApproval)
        {
            return Tier.HumanApproval;
        }

        return _ruleEngine.Evaluate(rule.Conditions, signals).Outcome == AccessEvaluationOutcome.Allow
            ? Tier.AutomaticAllows
            : Tier.AutomaticDenies;
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
