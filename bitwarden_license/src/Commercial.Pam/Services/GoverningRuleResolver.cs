using System.Text.Json;
using Bit.Pam.Services;
using Bit.Core.Repositories;
using Bit.Pam.Engine;
using Bit.Pam.Models;
using Bit.Pam.Models.Conditions;
using Bit.Pam.Repositories;

namespace Bit.Commercial.Pam.Services;

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

        // Deterministic order so ties between equally-favourable rules resolve to a stable governing collection.
        var governed = collections
            .Where(c => collectionIds.Contains(c.Id) && c.AccessRuleId.HasValue)
            .OrderBy(c => c.Id);

        // Least-restrictive wins: among the rules on the collections through which the caller reaches the cipher,
        // an automatic grant (Allow) is favoured over one needing human approval (RequiresApproval), which is
        // favoured over a denial (Deny). Evaluating each rule against the request signals means a failing automatic
        // rule (e.g. an out-of-range IP) never pre-empts a different path that would grant or route to a human.
        GoverningRule? best = null;
        var bestOutcome = AccessEvaluationOutcome.Deny;
        foreach (var collection in governed)
        {
            var accessRule = await _accessRuleRepository.GetByIdAsync(collection.AccessRuleId!.Value);
            if (accessRule is null)
            {
                continue;
            }

            var conditions = Parse(accessRule.Conditions);
            var outcome = _ruleEngine.Evaluate(conditions, signals).Outcome;
            if (best is not null && outcome >= bestOutcome)
            {
                continue;
            }

            bestOutcome = outcome;
            best = new GoverningRule(
                collection.OrganizationId,
                collection.Id,
                outcome == AccessEvaluationOutcome.RequiresApproval,
                conditions)
            {
                AllowsExtensions = accessRule.AllowsExtensions,
                MaxExtensionDurationSeconds = accessRule.MaxExtensionDurationSeconds,
            };

            if (outcome == AccessEvaluationOutcome.Allow)
            {
                // Nothing beats an automatic grant; stop scanning the remaining collections.
                break;
            }
        }

        return best;
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
