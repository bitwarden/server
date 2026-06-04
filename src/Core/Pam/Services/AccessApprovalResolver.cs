using System.Text.Json;
using Bit.Core.Pam.Models;
using Bit.Core.Pam.Models.Rules;
using Bit.Core.Pam.Repositories;
using Bit.Core.Repositories;

namespace Bit.Core.Pam.Services;

public class AccessApprovalResolver : IAccessApprovalResolver
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly ICollectionCipherRepository _collectionCipherRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IAccessRuleRepository _accessRuleRepository;

    public AccessApprovalResolver(
        ICollectionCipherRepository collectionCipherRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        _collectionCipherRepository = collectionCipherRepository;
        _collectionRepository = collectionRepository;
        _accessRuleRepository = accessRuleRepository;
    }

    public async Task<AccessApprovalResolution?> ResolveAsync(Guid userId, Guid cipherId)
    {
        var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdCipherIdAsync(userId, cipherId);
        if (collectionCiphers.Count == 0)
        {
            return null;
        }

        var collectionIds = collectionCiphers.Select(cc => cc.CollectionId).ToHashSet();
        var collections = await _collectionRepository.GetManyByManyIdsAsync(collectionIds);

        // Deterministic order so the chosen governing collection is stable across calls.
        var governed = collections
            .Where(c => collectionIds.Contains(c.Id) && c.AccessRuleId.HasValue)
            .OrderBy(c => c.Id);

        AccessApprovalResolution? automatic = null;
        foreach (var collection in governed)
        {
            var rule = await _accessRuleRepository.GetByIdAsync(collection.AccessRuleId!.Value);
            if (rule is null)
            {
                continue;
            }

            if (RequiresHumanApproval(rule.Rule))
            {
                // Most restrictive wins — return as soon as a human-approval rule is found.
                return new AccessApprovalResolution(collection.OrganizationId, collection.Id, true);
            }

            automatic ??= new AccessApprovalResolution(collection.OrganizationId, collection.Id, false);
        }

        return automatic;
    }

    /// <summary>
    /// True when the rule tree contains a human-approval node. A malformed or unparseable rule fails safe to true so
    /// access is never silently auto-approved on a rule the server could not understand.
    /// </summary>
    private static bool RequiresHumanApproval(string ruleJson)
    {
        Rule? rule;
        try
        {
            rule = JsonSerializer.Deserialize<Rule>(ruleJson, _jsonOptions);
        }
        catch (JsonException)
        {
            return true;
        }

        return rule is null || ContainsHumanApproval(rule);
    }

    private static bool ContainsHumanApproval(Rule rule) => rule switch
    {
        HumanApprovalRule => true,
        AllOfRule all => all.Rules.Any(ContainsHumanApproval),
        _ => false,
    };
}
