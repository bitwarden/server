using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Services;

#nullable enable
namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class SavePolicyCommand : ISavePolicyCommand
{
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IEventService _eventService;
    private readonly IPolicyRepository _policyRepository;
    private readonly IReadOnlyDictionary<PolicyType, IPolicyValidator> _policyValidators;
    private readonly TimeProvider _timeProvider;
    private readonly IOnPolicyPostUpsertEvent _onPolicyPostUpsertEvent;
    private readonly IPolicyEventHandlerFactory _policyEventHandlerFactory;

    public SavePolicyCommand(IApplicationCacheService applicationCacheService,
        IEventService eventService,
        IPolicyRepository policyRepository,
        IEnumerable<IPolicyValidator> policyValidators,
        TimeProvider timeProvider,
        IOnPolicyPostUpsertEvent onPolicyPostUpsertEvent,
        IPolicyEventHandlerFactory policyEventHandlerFactory)
    {
        _applicationCacheService = applicationCacheService;
        _eventService = eventService;
        _policyRepository = policyRepository;
        _timeProvider = timeProvider;
        _onPolicyPostUpsertEvent = onPolicyPostUpsertEvent;
        _policyEventHandlerFactory = policyEventHandlerFactory;

        var policyValidatorsDict = new Dictionary<PolicyType, IPolicyValidator>();
        foreach (var policyValidator in policyValidators)
        {
            if (!policyValidatorsDict.TryAdd(policyValidator.Type, policyValidator))
            {
                // Jimmy this check should happen earlier in the process.
                // It doesn’t make sense for this to be compile-time, but maybe a test to ensure this behavior.
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

        var policy =
            await _policyRepository.GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, policyUpdate.Type)
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

    public async Task<Policy> VNextSaveAsync(SavePolicyModel policyRequest)
    {
        var (_, currentPolicy) = await GetCurrentPolicyStateAsync(policyRequest.PolicyUpdate);

        var policy = await SaveAsync(policyRequest.PolicyUpdate);

        await ExecutePostPolicySaveSideEffectsForSupportedPoliciesAsync(policyRequest, policy, currentPolicy);

        return policy;
    }

    private async Task ExecutePostPolicySaveSideEffectsForSupportedPoliciesAsync(SavePolicyModel policyRequest,
        Policy postUpdatedPolicy, Policy? previousPolicyState)
    {
        if (postUpdatedPolicy.Type == PolicyType.OrganizationDataOwnership)
        {
            await _onPolicyPostUpsertEvent.ExecutePostUpsertSideEffectAsync(policyRequest, postUpdatedPolicy,
                previousPolicyState);
        }
    }


    private async Task RunValidatorAsync(IPolicyValidator validator, PolicyUpdate policyUpdate)
    {
        var (savedPoliciesDict, currentPolicy) = await GetCurrentPolicyStateAsync(policyUpdate);

        // If enabling this policy - check that all policy requirements are satisfied
        if (currentPolicy is not { Enabled: true } && policyUpdate.Enabled)
        {
            var missingRequiredPolicyTypes = validator.RequiredPolicies
                .Where(requiredPolicyType => savedPoliciesDict.GetValueOrDefault(requiredPolicyType) is not
                { Enabled: true })
                .ToList();

            if (missingRequiredPolicyTypes.Count != 0)
            {
                throw new BadRequestException(
                    $"Turn on the {missingRequiredPolicyTypes.First().GetName()} policy because it is required for the {validator.Type.GetName()} policy.");
            }
        }

        // If disabling this policy - ensure it's not required by any other policy
        if (currentPolicy is { Enabled: true } && !policyUpdate.Enabled)
        {
            var dependentPolicyTypes = _policyValidators.Values
                .Where(otherValidator => otherValidator.RequiredPolicies.Contains(policyUpdate.Type))
                .Select(otherValidator => otherValidator.Type)
                .Where(otherPolicyType => savedPoliciesDict.TryGetValue(otherPolicyType, out var savedPolicy) &&
                                          savedPolicy.Enabled)
                .ToList();

            switch (dependentPolicyTypes)
            {
                case { Count: 1 }:
                    throw new BadRequestException(
                        $"Turn off the {dependentPolicyTypes.First().GetName()} policy because it requires the {validator.Type.GetName()} policy.");
                case { Count: > 1 }:
                    throw new BadRequestException(
                        $"Turn off all of the policies that require the {validator.Type.GetName()} policy.");
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

    private async Task<(Dictionary<PolicyType, Policy> savedPoliciesDict, Policy? currentPolicy)>
        GetCurrentPolicyStateAsync(PolicyUpdate policyUpdate)
    {
        var savedPolicies = await _policyRepository.GetManyByOrganizationIdAsync(policyUpdate.OrganizationId);
        // Note: policies may be missing from this dict if they have never been enabled
        var savedPoliciesDict = savedPolicies.ToDictionary(p => p.Type);
        var currentPolicy = savedPoliciesDict.GetValueOrDefault(policyUpdate.Type);
        return (savedPoliciesDict, currentPolicy);
    }

    public async Task<Policy> V3SaveAsync(SavePolicyModel policyModel)
    {
        var policyUpdateRequest = policyModel.PolicyUpdate;
        await EnsureOrganizationCanUsePolicyAsync(policyUpdateRequest);

        var (savedPoliciesDict, currentPolicy) = await GetCurrentPolicyStateAsync(policyUpdateRequest);

        ValidatePolicyDependencies(policyUpdateRequest, currentPolicy, savedPoliciesDict);

        await ValidateTargetedPolicy(policyUpdateRequest, currentPolicy);

        await ExecutePreUpsertSideEffectAsync(policyUpdateRequest, currentPolicy);

        var upsertedPolicy = await UpsertPolicyAsync(policyUpdateRequest);

        await _eventService.LogPolicyEventAsync(upsertedPolicy, EventType.Policy_Updated);

        await ExecutePostUpsertSideEffectAsync(policyModel, upsertedPolicy, currentPolicy);

        return upsertedPolicy;
    }

    private async Task EnsureOrganizationCanUsePolicyAsync(PolicyUpdate policyUpdate)
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
    }

    private async Task<Policy> UpsertPolicyAsync(PolicyUpdate policyUpdateRequest)
    {
        var policy = await _policyRepository.GetByOrganizationIdTypeAsync(policyUpdateRequest.OrganizationId, policyUpdateRequest.Type)
                     ?? new Policy
                     {
                         OrganizationId = policyUpdateRequest.OrganizationId,
                         Type = policyUpdateRequest.Type,
                         CreationDate = _timeProvider.GetUtcNow().UtcDateTime
                     };

        policy.Enabled = policyUpdateRequest.Enabled;
        policy.Data = policyUpdateRequest.Data;
        policy.RevisionDate = _timeProvider.GetUtcNow().UtcDateTime;

        await _policyRepository.UpsertAsync(policy);

        return policy;
    }

    private async Task ValidateTargetedPolicy(PolicyUpdate policyUpdateRequest,
        Policy? currentPolicy)
    {
        var validator = _policyEventHandlerFactory.GetHandler<IPolicyValidationEvent>(currentPolicy!.Type);

        if (validator is null)
        {
            return;
        }

        var validationError = await validator.ValidateAsync(policyUpdateRequest, currentPolicy);
        if (!string.IsNullOrEmpty(validationError))
        {
            throw new BadRequestException(validationError);
        }
    }

    private void ValidatePolicyDependencies(PolicyUpdate policyUpdateRequest, Policy? currentPolicy,
        Dictionary<PolicyType, Policy> savedPoliciesDict)
    {
        var validator = _policyEventHandlerFactory.GetHandler<IEnforceDependentPoliciesEvent>(currentPolicy!.Type);

        if (validator is null)
        {
            return;
        }

        if (policyUpdateRequest.Enabled)
        {
            ValidateEnablingRequirements(validator, currentPolicy, savedPoliciesDict);
        }
        else
        {
            ValidateDisablingRequirements(validator, policyUpdateRequest, currentPolicy, savedPoliciesDict);
        }
    }


    private void ValidateDisablingRequirements(IEnforceDependentPoliciesEvent validator, PolicyUpdate policyUpdate,
        Policy? currentPolicy, Dictionary<PolicyType, Policy> savedPoliciesDict)
    {
        if (currentPolicy is not { Enabled: true })
            return;

        var dependentPolicyTypes = _policyValidators.Values
            .Where(otherValidator => otherValidator.RequiredPolicies.Contains(policyUpdate.Type))
            .Select(otherValidator => otherValidator.Type)
            .Where(otherPolicyType => savedPoliciesDict.TryGetValue(otherPolicyType, out var savedPolicy) &&
                                      savedPolicy.Enabled)
            .ToList();

        switch (dependentPolicyTypes)
        {
            case { Count: 1 }:
                throw new BadRequestException($"Turn off the {dependentPolicyTypes.First().GetName()} policy because it requires the {validator.Type.GetName()} policy.");
            case { Count: > 1 }:
                throw new BadRequestException($"Turn off all of the policies that require the {validator.Type.GetName()} policy.");
        }
    }

    private static void ValidateEnablingRequirements(IEnforceDependentPoliciesEvent validator, Policy? currentPolicy, Dictionary<PolicyType, Policy> savedPoliciesDict)
    {
        if (currentPolicy is { Enabled: true }) return;

        var missingRequiredPolicyTypes = validator.RequiredPolicies
            .Where(requiredPolicyType => savedPoliciesDict.GetValueOrDefault(requiredPolicyType) is not { Enabled: true })
            .ToList();

        if (missingRequiredPolicyTypes.Count != 0)
        {
            throw new BadRequestException($"Turn on the {missingRequiredPolicyTypes.First().GetName()} policy because it is required for the {validator.Type.GetName()} policy.");
        }
    }

    private async Task ExecutePreUpsertSideEffectAsync(PolicyUpdate policyRequest, Policy? currentPolicy)
    {
        var handler = _policyEventHandlerFactory.GetHandler<IOnPolicyPreUpsertEvent>(policyRequest.Type);

        if (handler is null)
        {
            return;
        }
        // Jimmy, one of lib optional type.
        await handler.ExecutePreUpsertSideEffectAsync(policyRequest, currentPolicy);
    }

    private async Task ExecutePostUpsertSideEffectAsync(SavePolicyModel policyRequest,
        Policy postUpdatedPolicy, Policy? previousPolicyState)
    {
        var handler = _policyEventHandlerFactory.GetHandler<IOnPolicyPostUpsertEvent>(policyRequest.PolicyUpdate.Type);

        if (handler is null)
        {
            return;
        }

        await handler.ExecutePostUpsertSideEffectAsync(policyRequest, postUpdatedPolicy, previousPolicyState);
    }


}

