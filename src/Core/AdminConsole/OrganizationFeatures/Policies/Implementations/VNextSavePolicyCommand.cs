using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class VNextSavePolicyCommand(
    IApplicationCacheService applicationCacheService,
    IEventService eventService,
    IPolicyRepository policyRepository,
    IEnumerable<IEnforceDependentPoliciesEvent> policyValidationEventHandlers,
    TimeProvider timeProvider,
    IPolicyEventHandlerFactory policyEventHandlerFactory)
    : IVNextSavePolicyCommand
{
    private readonly IReadOnlyDictionary<PolicyType, IEnforceDependentPoliciesEvent> _policyValidationEvents = MapToDictionary(policyValidationEventHandlers);

    private static Dictionary<PolicyType, IEnforceDependentPoliciesEvent> MapToDictionary(IEnumerable<IEnforceDependentPoliciesEvent> policyValidationEventHandlers)
    {
        var policyValidationEventsDict = new Dictionary<PolicyType, IEnforceDependentPoliciesEvent>();
        foreach (var policyValidationEvent in policyValidationEventHandlers)
        {
            if (!policyValidationEventsDict.TryAdd(policyValidationEvent.Type, policyValidationEvent))
            {
                throw new Exception($"Duplicate PolicyValidationEvent for {policyValidationEvent.Type} policy.");
            }
        }
        return policyValidationEventsDict;
    }

    public async Task<Policy> SaveAsync(SavePolicyModel policyRequest)
    {
        var policyUpdateRequest = policyRequest.PolicyUpdate;
        var organizationId = policyUpdateRequest.OrganizationId;

        await EnsureOrganizationCanUsePolicyAsync(organizationId);

        var savedPoliciesDict = await GetCurrentPolicyStateAsync(organizationId);

        var currentPolicy = savedPoliciesDict.GetValueOrDefault(policyUpdateRequest.Type);

        ValidatePolicyDependencies(policyUpdateRequest, currentPolicy, savedPoliciesDict);

        await ValidateTargetedPolicyAsync(policyRequest, currentPolicy);

        await ExecutePreUpsertSideEffectAsync(policyRequest, currentPolicy);

        var upsertedPolicy = await UpsertPolicyAsync(policyUpdateRequest);

        await eventService.LogPolicyEventAsync(upsertedPolicy, EventType.Policy_Updated);

        await ExecutePostUpsertSideEffectAsync(policyRequest, upsertedPolicy, currentPolicy);

        return upsertedPolicy;
    }

    private async Task EnsureOrganizationCanUsePolicyAsync(Guid organizationId)
    {
        var org = await applicationCacheService.GetOrganizationAbilityAsync(organizationId);
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
        var policy = await policyRepository.GetByOrganizationIdTypeAsync(policyUpdateRequest.OrganizationId, policyUpdateRequest.Type)
                     ?? new Policy
                     {
                         OrganizationId = policyUpdateRequest.OrganizationId,
                         Type = policyUpdateRequest.Type,
                         CreationDate = timeProvider.GetUtcNow().UtcDateTime
                     };

        policy.Enabled = policyUpdateRequest.Enabled;
        policy.Data = policyUpdateRequest.Data;
        policy.RevisionDate = timeProvider.GetUtcNow().UtcDateTime;

        await policyRepository.UpsertAsync(policy);

        return policy;
    }

    private async Task ValidateTargetedPolicyAsync(SavePolicyModel policyRequest,
        Policy? currentPolicy)
    {
        await ExecutePolicyEventAsync<IPolicyValidationEvent>(
            policyRequest.PolicyUpdate.Type,
            async validator =>
            {
                var validationError = await validator.ValidateAsync(policyRequest, currentPolicy);
                if (!string.IsNullOrEmpty(validationError))
                {
                    throw new BadRequestException(validationError);
                }
            });
    }

    private void ValidatePolicyDependencies(
        PolicyUpdate policyUpdateRequest,
        Policy? currentPolicy,
        Dictionary<PolicyType, Policy> savedPoliciesDict)
    {
        var result = policyEventHandlerFactory.GetHandler<IEnforceDependentPoliciesEvent>(policyUpdateRequest.Type);

        result.Switch(
            validator =>
            {
                var isCurrentlyEnabled = currentPolicy?.Enabled == true;

                switch (policyUpdateRequest.Enabled)
                {
                    case true when !isCurrentlyEnabled:
                        ValidateEnablingRequirements(validator, savedPoliciesDict);
                        return;
                    case false when isCurrentlyEnabled:
                        ValidateDisablingRequirements(validator, policyUpdateRequest.Type, savedPoliciesDict);
                        break;
                }
            },
            _ => { });
    }

    private void ValidateDisablingRequirements(
        IEnforceDependentPoliciesEvent validator,
        PolicyType policyType,
        Dictionary<PolicyType, Policy> savedPoliciesDict)
    {
        var dependentPolicyTypes = _policyValidationEvents.Values
            .Where(otherValidator => otherValidator.RequiredPolicies.Contains(policyType))
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

    private static void ValidateEnablingRequirements(
        IEnforceDependentPoliciesEvent validator,
        Dictionary<PolicyType, Policy> savedPoliciesDict)
    {
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
        var handler = policyEventHandlerFactory.GetHandler<T>(type);

        await handler.Match(
            async h => await func(h),
            _ => Task.CompletedTask
        );
    }

    private async Task<Dictionary<PolicyType, Policy>> GetCurrentPolicyStateAsync(Guid organizationId)
    {
        var savedPolicies = await policyRepository.GetManyByOrganizationIdAsync(organizationId);
        // Note: policies may be missing from this dict if they have never been enabled
        var savedPoliciesDict = savedPolicies.ToDictionary(p => p.Type);
        return savedPoliciesDict;
    }
}
