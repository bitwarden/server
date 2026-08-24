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

        var governedCollections = collections
            .Where(c => collectionIds.Contains(c.Id) && c.AccessRuleId.HasValue);

        // Load every rule on the collections through which the caller reaches the cipher, keeping each paired with
        // the collection it gates. A rule is dropped — so it stops governing — when it is disabled (Enabled is false;
        // the admin has switched it off, and a disabled rule does not gate access) or no longer loads (deleted after
        // the collection was read; deletes clear the link, so a missing rule is only a race). Dropping a disabled rule
        // also stops it shadowing a newer active rule under the oldest-wins selection below.
        var candidates = new List<(Collection Collection, AccessRule Rule)>();
        foreach (var collection in governedCollections)
        {
            var accessRule = await _accessRuleRepository.GetByIdAsync(collection.AccessRuleId!.Value);
            if (accessRule is { Enabled: true })
            {
                candidates.Add((collection, accessRule));
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        // Oldest wins: the rule with the earliest CreationDate governs, ties broken on rule id so the choice is total
        // and stable. Selection is purely structural — it does NOT depend on how a rule's conditions evaluate for the
        // current signals — so a newer path never pre-empts an older one, whichever is the more permissive. This is a
        // deliberate trade of determinism over least-restriction: a member may be routed to an approver even though a
        // newer path would have auto-granted, because the older rule governs.
        var (governingCollection, governingRule) = candidates
            .OrderBy(c => c.Rule.CreationDate)
            .ThenBy(c => c.Rule.Id)
            .First();

        return Build(governingCollection.OrganizationId, governingCollection.Id, governingRule);
    }

    public async Task<GoverningRule?> ResolvePinnedAsync(Guid ruleId, Guid collectionId)
    {
        var rule = await _accessRuleRepository.GetByIdAsync(ruleId);

        // Dropped for the same two reasons ResolveAsync drops a candidate, and to the same effect: a disabled rule does
        // not gate access, and a rule that no longer loads has been deleted. Either way the pin points at nothing that
        // governs any more, so the caller is left ungated rather than held to a rule the admin took out of service.
        return rule is { Enabled: true } ? Build(rule.OrganizationId, collectionId, rule) : null;
    }

    /// <summary>
    /// Projects a stored rule onto the shape its callers evaluate. Shared by both resolution paths so a rule reached
    /// through the caller's collections and the same rule reached through a request's pin can never be described
    /// differently — the pinned path exists precisely so a later operation sees the rule that decided, and that
    /// guarantee is worth nothing if the two paths read its fields differently.
    /// </summary>
    private static GoverningRule Build(Guid organizationId, Guid collectionId, AccessRule rule)
    {
        var (conditions, unreadable) = Parse(rule.Conditions);

        // Whether the rule routes to a human is structural too: it is carried by a HumanApprovalCondition among the
        // rule's conditions, not by how those conditions evaluate for these signals. Reading it off the engine's
        // verdict asked the wrong question — Combine gives deny precedence over requires-approval, so one denying
        // condition (an IP outside the allowlist, a request outside the time windows) folded the whole rule to Deny
        // and reported "no approval needed", sending a human-gated rule down the automatic path to be refused
        // outright instead of to an approver (PM-42256). The conditions ride along on the returned rule; the
        // automatic path is where they are evaluated.
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
    /// to fall back. A malformed or unparseable document fails safe to a single human-approval condition so access is
    /// never silently auto-approved on conditions the server could not understand; the human-approval path then routes
    /// it to an approver rather than issuing an automatic lease. The flag rides alongside because that stand-in is
    /// indistinguishable from a genuine <c>[human_approval]</c> rule, and a caller that strips the approval gate
    /// before evaluating (see <see cref="GoverningRule.AutomatedConditions"/>) is left with an empty list, which the
    /// engine reads as vacuously satisfied — the fail-safe would become a fail-open without something to mark it.
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
