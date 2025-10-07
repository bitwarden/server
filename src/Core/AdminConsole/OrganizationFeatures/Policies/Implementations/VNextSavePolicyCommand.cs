using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class VNextSavePolicyCommand : IVNextSavePolicyCommand
{
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IEventService _eventService;
    private readonly IPolicyRepository _policyRepository;
    private readonly IReadOnlyDictionary<PolicyType, IPolicyValidator> _policyValidators;
    private readonly TimeProvider _timeProvider;
    private readonly IPolicyEventHandlerFactory _policyEventHandlerFactory;

    public VNextSavePolicyCommand(
        IApplicationCacheService applicationCacheService,
        IEventService eventService,
        IPolicyRepository policyRepository,
        IEnumerable<IPolicyValidator> policyValidators,
        TimeProvider timeProvider,
        IPolicyEventHandlerFactory policyEventHandlerFactory)
    {
        _applicationCacheService = applicationCacheService;
        _eventService = eventService;
        _policyRepository = policyRepository;
        _timeProvider = timeProvider;
        _policyEventHandlerFactory = policyEventHandlerFactory;

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

    public async Task<Policy> SaveAsync(SavePolicyModel policyRequest)
    {
        var policyUpdateRequest = policyRequest.PolicyUpdate;

        await EnsureOrganizationCanUsePolicyAsync(policyUpdateRequest.OrganizationId);

        var (savedPoliciesDict, currentPolicy) = await GetCurrentPolicyStateAsync(policyUpdateRequest);

        ValidatePolicyDependencies(policyUpdateRequest, currentPolicy, savedPoliciesDict);

        await ValidateTargetedPolicyAsync(policyUpdateRequest, currentPolicy);

        await ExecutePreUpsertSideEffectAsync(policyRequest, currentPolicy);

        var upsertedPolicy = await UpsertPolicyAsync(policyUpdateRequest);

        await _eventService.LogPolicyEventAsync(upsertedPolicy, EventType.Policy_Updated);

        await ExecutePostUpsertSideEffectAsync(policyRequest, upsertedPolicy, currentPolicy);


        return upsertedPolicy;
    }

    private async Task EnsureOrganizationCanUsePolicyAsync(Guid organizationId)
    {
        var org = await _applicationCacheService.GetOrganizationAbilityAsync(organizationId);
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

    private async Task ValidateTargetedPolicyAsync(PolicyUpdate policyUpdateRequest,
        Policy? currentPolicy)
    {
        await ExecutePolicyEventAsync<IPolicyValidationEvent>(
            currentPolicy!.Type,
            async validator =>
            {
                var validationError = await validator.ValidateAsync(policyUpdateRequest, currentPolicy);
                if (!string.IsNullOrEmpty(validationError))
                {
                    throw new BadRequestException(validationError);
                }
            });
    }

    private void ValidatePolicyDependencies(PolicyUpdate policyUpdateRequest, Policy? currentPolicy,
        Dictionary<PolicyType, Policy> savedPoliciesDict)
    {
        var result = _policyEventHandlerFactory.GetHandler<IEnforceDependentPoliciesEvent>(currentPolicy!.Type);

        result.Switch(
            validator =>
            {
                if (policyUpdateRequest.Enabled)
                {
                    ValidateEnablingRequirements(validator, currentPolicy, savedPoliciesDict);
                }
                else
                {
                    ValidateDisablingRequirements(validator, policyUpdateRequest, currentPolicy, savedPoliciesDict);
                }
            },
            _ => { });
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

    private async Task ExecutePreUpsertSideEffectAsync(
        SavePolicyModel policyRequest,
        Policy? currentPolicy)
    {
        await ExecutePolicyEventAsync<IOnPolicyPreUpdateEvent>(
            policyRequest.PolicyUpdate.Type,
            handler => handler.ExecutePreUpsertSideEffectAsync(policyRequest, currentPolicy));
    }
    private async Task ExecutePostUpsertSideEffectAsync(
        SavePolicyModel policyRequest,
        Policy postUpsertedPolicyState,
        Policy? previousPolicyState)
    {
        await ExecutePolicyEventAsync<IOnPolicyPostUpdateEvent>(
            policyRequest.PolicyUpdate.Type,
            handler => handler.ExecutePostUpsertSideEffectAsync(
                policyRequest,
                postUpsertedPolicyState,
                previousPolicyState));
    }

    private async Task ExecutePolicyEventAsync<T>(PolicyType type, Func<T, Task> func) where T : IPolicyUpdateEvent
    {
        var handler = _policyEventHandlerFactory.GetHandler<T>(type);

        await handler.Match(
            async h => await func(h),
            _ => Task.CompletedTask
        );
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
}
