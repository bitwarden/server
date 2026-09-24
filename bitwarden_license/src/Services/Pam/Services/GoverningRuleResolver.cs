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

        // Gating is a union: every path the caller can reach the cipher through must gate. A path carrying no rule,
        // a disabled one, or one that no longer loads is an escape releasing the credential in full, so nothing
        // governs — matching CipherLeaseGate's bulk read and SingleActiveLeaseEvaluator (PM-42916).
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

        // Oldest wins: the rule with the earliest CreationDate governs, ties broken on rule id, regardless of
        // whether a newer path would have been more permissive.
        var (governingCollection, governingRule) = candidates
            .OrderBy(c => c.Rule.CreationDate)
            .ThenBy(c => c.Rule.Id)
            .First();

        return Build(governingCollection.OrganizationId, governingCollection.Id, governingRule);
    }

    public async Task<GoverningRule?> ResolvePinnedAsync(Guid ruleId, Guid collectionId)
    {
        var rule = await _accessRuleRepository.GetByIdAsync(ruleId);

        // Dropped as ResolveAsync drops an escape path: a disabled or deleted rule leaves the pin governing
        // nothing, so the caller is left ungated rather than held to a rule the admin took out of service.
        return rule is { Enabled: true } ? Build(rule.OrganizationId, collectionId, rule) : null;
    }

    /// <summary>
    /// Projects a stored rule onto the shape its callers evaluate. Shared by both resolution paths so a rule reached
    /// through the caller's collections and the same rule reached through a request's pin are always described
    /// identically.
    /// </summary>
    private static GoverningRule Build(Guid organizationId, Guid collectionId, AccessRule rule)
    {
        var (conditions, unreadable) = Parse(rule.Conditions);

        // Whether the rule routes to a human is structural: carried by a HumanApprovalCondition among the rule's
        // conditions, not by how those conditions evaluate for these signals (Combine gives deny precedence over
        // requires-approval, which would fold a human-gated rule to an outright Deny).
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
    /// Parses the stored conditions JSON into a flat list of <see cref="AccessCondition"/>, reporting whether it had
    /// to fall back. A malformed or unparseable document fails safe to a single human-approval condition, since the
    /// flag is needed to tell that stand-in apart from a genuine <c>[human_approval]</c> rule.
    /// </summary>
    private static (IReadOnlyList<AccessCondition> Conditions, bool Unreadable) Parse(string conditionsJson)
    {
        try
        {
            var conditions = JsonSerializer.Deserialize<List<AccessCondition>>(conditionsJson, AccessConditionJson.Options);
            return conditions is null ? FailSafe() : (conditions, false);
        }
        // NotSupportedException alongside JsonException: the polymorphic reader reports a missing or unreadable
        // "kind" that way, and it is not a JsonException. Left uncaught it would escape ResolveAsync entirely,
        // breaking the fail-safe this method exists to provide — a stored document the server cannot interpret has
        // to route to an approver, not surface as an unhandled exception.
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return FailSafe();
        }
    }

    private static (IReadOnlyList<AccessCondition> Conditions, bool Unreadable) FailSafe() =>
        ([new HumanApprovalCondition()], true);
}
