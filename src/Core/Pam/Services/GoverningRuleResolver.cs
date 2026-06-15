using System.Text.Json;
using Bit.Core.Pam.Models;
using Bit.Core.Pam.Models.Conditions;
using Bit.Core.Pam.Repositories;
using Bit.Core.Repositories;

namespace Bit.Core.Pam.Services;

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

    public GoverningRuleResolver(
        ICollectionCipherRepository collectionCipherRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        _collectionCipherRepository = collectionCipherRepository;
        _collectionRepository = collectionRepository;
        _accessRuleRepository = accessRuleRepository;
    }

    public async Task<GoverningRule?> ResolveAsync(Guid userId, Guid cipherId)
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

        GoverningRule? automatic = null;
        foreach (var collection in governed)
        {
            var accessRule = await _accessRuleRepository.GetByIdAsync(collection.AccessRuleId!.Value);
            if (accessRule is null)
            {
                continue;
            }

            var condition = Parse(accessRule.Conditions);
            if (ContainsHumanApproval(condition))
            {
                // Most restrictive wins — return as soon as a human-approval condition is found.
                return new GoverningRule(collection.OrganizationId, collection.Id, true, condition)
                {
                    AllowsExtensions = accessRule.AllowsExtensions,
                    MaxExtensionDurationSeconds = accessRule.MaxExtensionDurationSeconds,
                };
            }

            automatic ??= new GoverningRule(collection.OrganizationId, collection.Id, false, condition)
            {
                AllowsExtensions = accessRule.AllowsExtensions,
                MaxExtensionDurationSeconds = accessRule.MaxExtensionDurationSeconds,
            };
        }

        return automatic;
    }

    /// <summary>
    /// Parses the stored conditions JSON into an <see cref="AccessCondition"/>. A malformed or unparseable document
    /// fails safe to a <see cref="HumanApprovalCondition"/> so access is never silently auto-approved on conditions
    /// the server could not understand; the human-approval path then routes it to an approver rather than issuing an
    /// automatic lease.
    /// </summary>
    private static AccessCondition Parse(string conditionsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<AccessCondition>(conditionsJson, _jsonOptions) ?? new HumanApprovalCondition();
        }
        catch (JsonException)
        {
            return new HumanApprovalCondition();
        }
    }

    private static bool ContainsHumanApproval(AccessCondition condition) => condition switch
    {
        HumanApprovalCondition => true,
        AllOfCondition all => all.Conditions.Any(ContainsHumanApproval),
        _ => false,
    };
}
