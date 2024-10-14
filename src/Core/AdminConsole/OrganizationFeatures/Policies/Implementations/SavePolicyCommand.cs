#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class SavePolicyCommand : ISavePolicyCommand
{
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IEventService _eventService;
    private readonly IPolicyRepository _policyRepository;
    private readonly IReadOnlyDictionary<PolicyType, IPolicyValidator> _policyValidators;

    public SavePolicyCommand(
        IApplicationCacheService applicationCacheService,
        IEventService eventService,
        IPolicyRepository policyRepository,
        IEnumerable<IPolicyValidator> policyValidators)
    {
        _applicationCacheService = applicationCacheService;
        _eventService = eventService;
        _policyRepository = policyRepository;

        var policyValidatorsDict = new Dictionary<PolicyType, IPolicyValidator>();
        foreach (var policyValidator in policyValidators)
        {
            if (!policyValidatorsDict.TryAdd(policyValidator.Type, policyValidator))
            {
                throw new Exception($"Duplicate PolicyValidator for {policyValidator.Type} policy.");
            }
        }

        _policyValidators = policyValidatorsDict;
    }

    public async Task SaveAsync(PolicyUpdate policyUpdate, IOrganizationService organizationService, Guid? savingUserId)
    {
        var org = await _applicationCacheService.GetOrganizationAbilityAsync(policyUpdate.OrganizationId);
        if (org == null)
        {
            throw new BadRequestException("Organization not found");
        }

        if (!org.UsePolicies)
        {
            throw new BadRequestException("This organization cannot use policies.");
        }

        if (_policyValidators.TryGetValue(policyUpdate.Type, out var validator))
        {
            await RunValidatorAsync(validator, policyUpdate, organizationService);
        }

        var policy = await _policyRepository.GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, policyUpdate.Type)
                     ?? new Policy
                     {
                         OrganizationId = policyUpdate.OrganizationId,
                         Type = policyUpdate.Type
                     };

        policy.Enabled = policyUpdate.Enabled;
        policy.Data = policyUpdate.Data;
        policy.RevisionDate = DateTime.UtcNow;

        await _policyRepository.UpsertAsync(policy);
        await _eventService.LogPolicyEventAsync(policy, EventType.Policy_Updated);
    }

    private async Task RunValidatorAsync(IPolicyValidator validator, PolicyUpdate policyUpdate, IOrganizationService organizationService)
    {
        var allSavedPolicies = await _policyRepository.GetManyByOrganizationIdAsync(policyUpdate.OrganizationId);
        var currentPolicy = allSavedPolicies.SingleOrDefault(p => p.Type == policyUpdate.Type);

        // If enabling this policy - check that all policy requirements are satisfied
        if (currentPolicy is not { Enabled: true } && policyUpdate.Enabled)
        {
            var missingRequiredPolicyTypes = validator.RequiredPolicies
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
        if (currentPolicy is { Enabled: true } && !policyUpdate.Enabled)
        {
            var dependentPolicies = _policyValidators.Values
                .Where(otherValidator => otherValidator.RequiredPolicies.Contains(policyUpdate.Type))
                .Select(otherValidator => otherValidator.Type)
                .Select(otherPolicyType => allSavedPolicies.SingleOrDefault(p => p.Type == otherPolicyType))
                .Where(otherPolicy => otherPolicy is { Enabled: true })
                .ToList();

            if (dependentPolicies is { Count: > 0 })
            {
                throw new BadRequestException("This policy is required by " + dependentPolicies.First()!.Type + " policy. Try disabling that policy first.");
            }
        }

        // Run other validation
        var validationError = await validator.ValidateAsync(policyUpdate, currentPolicy);
        if (!string.IsNullOrEmpty(validationError))
        {
            throw new BadRequestException(validationError);
        }

        // Run side effects
        await validator.OnSaveSideEffectsAsync(policyUpdate, currentPolicy, organizationService);
    }
}
