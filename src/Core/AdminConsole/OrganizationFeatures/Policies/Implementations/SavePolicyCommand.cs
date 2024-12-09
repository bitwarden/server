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
    private readonly TimeProvider _timeProvider;

    public SavePolicyCommand(
        IApplicationCacheService applicationCacheService,
        IEventService eventService,
        IPolicyRepository policyRepository,
        IEnumerable<IPolicyValidator> policyValidators,
        TimeProvider timeProvider)
    {
        _applicationCacheService = applicationCacheService;
        _eventService = eventService;
        _policyRepository = policyRepository;
        _timeProvider = timeProvider;

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

    public async Task<Policy> SaveAsync(PolicyUpdate policyUpdate)
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
            await RunValidatorAsync(validator, policyUpdate);
        }

        var policy = await _policyRepository.GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, policyUpdate.Type)
                     ?? new Policy
                     {
                         OrganizationId = policyUpdate.OrganizationId,
                         Type = policyUpdate.Type,
                         CreationDate = _timeProvider.GetUtcNow().UtcDateTime
                     };

        policy.Enabled = policyUpdate.Enabled;
        policy.Data = policyUpdate.Data;
        policy.RevisionDate = _timeProvider.GetUtcNow().UtcDateTime;

        await _policyRepository.UpsertAsync(policy);
        await _eventService.LogPolicyEventAsync(policy, EventType.Policy_Updated);

        return policy;
    }

    private async Task RunValidatorAsync(IPolicyValidator validator, PolicyUpdate policyUpdate)
    {
        var savedPolicies = await _policyRepository.GetManyByOrganizationIdAsync(policyUpdate.OrganizationId);
        // Note: policies may be missing from this dict if they have never been enabled
        var savedPoliciesDict = savedPolicies.ToDictionary(p => p.Type);
        var currentPolicy = savedPoliciesDict.GetValueOrDefault(policyUpdate.Type);

        // If enabling this policy - check that all policy requirements are satisfied
        if (currentPolicy is not { Enabled: true } && policyUpdate.Enabled)
        {
            var missingRequiredPolicyTypes = validator.RequiredPolicies
                .Where(requiredPolicyType => savedPoliciesDict.GetValueOrDefault(requiredPolicyType) is not { Enabled: true })
                .ToList();

            if (missingRequiredPolicyTypes.Count != 0)
            {
                throw new BadRequestException($"Turn on the {missingRequiredPolicyTypes.First().GetName()} policy because it is required for the {validator.Type.GetName()} policy.");
            }
        }

        // If disabling this policy - ensure it's not required by any other policy
        if (currentPolicy is { Enabled: true } && !policyUpdate.Enabled)
        {
            var dependentPolicyTypes = _policyValidators.Values
                .Where(otherValidator => otherValidator.RequiredPolicies.Contains(policyUpdate.Type))
                .Select(otherValidator => otherValidator.Type)
                .Where(otherPolicyType => savedPoliciesDict.ContainsKey(otherPolicyType) &&
                    savedPoliciesDict[otherPolicyType].Enabled)
                .ToList();

            switch (dependentPolicyTypes)
            {
                case { Count: 1 }:
                    throw new BadRequestException($"Turn off the {dependentPolicyTypes.First().GetName()} policy because it requires the {validator.Type.GetName()} policy.");
                case { Count: > 1 }:
                    throw new BadRequestException($"Turn off all of the policies that require the {validator.Type.GetName()} policy.");
            }
        }

        // Run other validation
        var validationError = await validator.ValidateAsync(policyUpdate, currentPolicy);
        if (!string.IsNullOrEmpty(validationError))
        {
            throw new BadRequestException(validationError);
        }

        // Run side effects
        await validator.OnSaveSideEffectsAsync(policyUpdate, currentPolicy);
    }
}
