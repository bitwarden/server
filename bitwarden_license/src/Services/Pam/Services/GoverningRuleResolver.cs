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
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

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

        var governedCollections = collections
            .Where(c => collectionIds.Contains(c.Id) && c.AccessRuleId.HasValue);

        // Load every rule on the collections through which the caller reaches the cipher, keeping each paired with
        // the collection it gates. A rule that no longer loads (e.g. a soft-deleted one, which the read filters out)
        // is skipped, so a deleted rule stops governing.
        var candidates = new List<(Collection Collection, AccessRule Rule)>();
        foreach (var collection in governedCollections)
        {
            var accessRule = await _accessRuleRepository.GetByIdAsync(collection.AccessRuleId!.Value);
            if (accessRule is not null)
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
        // newer path would have auto-granted, because the older rule governs. The chosen rule's conditions are then
        // evaluated below only to decide whether it routes to a human or resolves automatically.
        var (governingCollection, governingRule) = candidates
            .OrderBy(c => c.Rule.CreationDate)
            .ThenBy(c => c.Rule.Id)
            .First();

        var conditions = Parse(governingRule.Conditions);
        var outcome = _ruleEngine.Evaluate(conditions, signals).Outcome;

        return new GoverningRule(
            governingCollection.OrganizationId,
            governingCollection.Id,
            outcome == AccessEvaluationOutcome.RequiresApproval,
            conditions)
        {
            RuleId = governingRule.Id,
            AllowsExtensions = governingRule.AllowsExtensions,
            MaxExtensionDurationSeconds = governingRule.MaxExtensionDurationSeconds,
        };
    }

    /// <summary>
    /// Parses the stored conditions JSON into a flat list of <see cref="AccessCondition"/>. A malformed or
    /// unparseable document fails safe to a single human-approval condition so access is never silently auto-approved
    /// on conditions the server could not understand; the human-approval path then routes it to an approver rather
    /// than issuing an automatic lease.
    /// </summary>
    private static IReadOnlyList<AccessCondition> Parse(string conditionsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<AccessCondition>>(conditionsJson, _jsonOptions) ?? FailSafe();
        }
        catch (JsonException)
        {
            return FailSafe();
        }
    }

    private static IReadOnlyList<AccessCondition> FailSafe() => [new HumanApprovalCondition()];
}
