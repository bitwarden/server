#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.Services.Implementations;

public class PolicyServicevNext : IPolicyServicevNext
{
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IEventService _eventService;
    private readonly IPolicyRepository _policyRepository;
    private readonly IReadOnlyDictionary<PolicyType, IPolicyDefinition> _policyDefinitions;

    public PolicyServicevNext(
        IApplicationCacheService applicationCacheService,
        IEventService eventService,
        IPolicyRepository policyRepository,
        IEnumerable<IPolicyDefinition> policyDefinitions)
    {
        _applicationCacheService = applicationCacheService;
        _eventService = eventService;
        _policyRepository = policyRepository;

        var policyDefinitionsDict = new Dictionary<PolicyType, IPolicyDefinition>();
        foreach (var policyDefinition in policyDefinitions)
        {
            if (!policyDefinitionsDict.TryAdd(policyDefinition.Type, policyDefinition))
            {
                throw new Exception($"Duplicate PolicyDefinition for {policyDefinition.Type} policy.");
            }
        }

        _policyDefinitions = policyDefinitionsDict;
    }

    public async Task SaveAsync(Policy policy, IUserService userService, IOrganizationService organizationService,
        Guid? savingUserId)
    {
        var org = await _applicationCacheService.GetOrganizationAbilityAsync(policy.OrganizationId);
        if (org == null)
        {
            throw new BadRequestException("Organization not found");
        }

        if (!org.UsePolicies)
        {
            throw new BadRequestException("This organization cannot use policies.");
        }

        var policyDefinition = GetDefinition(policy.Type);
        var allSavedPolicies = await _policyRepository.GetManyByOrganizationIdAsync(org.Id);
        var currentPolicy = allSavedPolicies.SingleOrDefault(p => p.Id == policy.Id);

        // If enabling this policy - check that all policy requirements are satisfied
        if (currentPolicy is not { Enabled: true } && policy.Enabled)
        {
            var missingRequiredPolicyTypes = policyDefinition.RequiredPolicies
                .Where(requiredPolicyType =>
                    allSavedPolicies.SingleOrDefault(p => p.Type == requiredPolicyType) is not { Enabled: true })
                .ToList();

            if (missingRequiredPolicyTypes.Count != 0)
            {
                // TODO: would be better to reference the name instead of the enum
                throw new BadRequestException("Policy requires PolicyType " + missingRequiredPolicyTypes.First() + " to be enabled first.");
            }
        }

        // If disabling this policy - ensure it's not required by any other policy
        if (currentPolicy is { Enabled: true } && !policy.Enabled)
        {
            var dependentPolicies = _policyDefinitions.Values
                .Where(policyDef => policyDef.RequiredPolicies.Contains(policy.Type))
                .Select(policyDef => policyDef.Type)
                .Select(otherPolicyType => allSavedPolicies.SingleOrDefault(p => p.Type == otherPolicyType))
                .Where(otherPolicy => otherPolicy is { Enabled: true })
                .ToList();

            if (dependentPolicies is { Count: > 0 })
            {
                throw new BadRequestException("This policy is required by " + dependentPolicies.First() + ". Try disabling that policy first.");
            }
        }

        // Run other validation
        var validationError = await policyDefinition.Validate(currentPolicy, policy);
        if (validationError != null)
        {
            throw new BadRequestException(validationError);
        }

        // Run side effects
        await policyDefinition.OnSaveSideEffects(currentPolicy, policy);

        var now = DateTime.UtcNow;
        if (policy.Id == default)
        {
            policy.CreationDate = now;
        }

        policy.RevisionDate = now;

        await _policyRepository.UpsertAsync(policy);
        await _eventService.LogPolicyEventAsync(policy, EventType.Policy_Updated);
    }

    private IPolicyDefinition GetDefinition(PolicyType type)
    {
        if (!_policyDefinitions.TryGetValue(type, out var result))
        {
            throw new Exception($"No PolicyDefinition found for {type} policy.");
        }

        return result;
    }
}
