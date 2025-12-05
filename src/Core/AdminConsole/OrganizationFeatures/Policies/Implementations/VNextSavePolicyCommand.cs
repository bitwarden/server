using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models;
using Bit.Core.Platform.Push;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class VNextSavePolicyCommand(
    IApplicationCacheService applicationCacheService,
    IEventService eventService,
    IPolicyRepository policyRepository,
    IEnumerable<IPolicyUpdateEvent> policyUpdateEventHandlers,
    TimeProvider timeProvider,
    IPolicyEventHandlerFactory policyEventHandlerFactory,
    IPushNotificationService pushNotificationService)
    : IVNextSavePolicyCommand
{

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
        await PushPolicyUpdateToClients(policyUpdateRequest.OrganizationId, policy);
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
        var isCurrentlyEnabled = currentPolicy?.Enabled == true;
        var isBeingEnabled = policyUpdateRequest.Enabled && !isCurrentlyEnabled;
        var isBeingDisabled = !policyUpdateRequest.Enabled && isCurrentlyEnabled;

        if (isBeingEnabled)
        {
            ValidateEnablingRequirements(policyUpdateRequest.Type, savedPoliciesDict);
        }
        else if (isBeingDisabled)
        {
            ValidateDisablingRequirements(policyUpdateRequest.Type, savedPoliciesDict);
        }
    }

    private void ValidateDisablingRequirements(
        PolicyType policyType,
        Dictionary<PolicyType, Policy> savedPoliciesDict)
    {
        var dependentPolicyTypes = policyUpdateEventHandlers
            .OfType<IEnforceDependentPoliciesEvent>()
            .Where(otherValidator => otherValidator.RequiredPolicies.Contains(policyType))
            .Select(otherValidator => otherValidator.Type)
            .Where(otherPolicyType => savedPoliciesDict.TryGetValue(otherPolicyType, out var savedPolicy) &&
                                      savedPolicy.Enabled)
            .ToList();

        switch (dependentPolicyTypes)
        {
            case { Count: 1 }:
                throw new BadRequestException($"Turn off the {dependentPolicyTypes.First().GetName()} policy because it requires the {policyType.GetName()} policy.");
            case { Count: > 1 }:
                throw new BadRequestException($"Turn off all of the policies that require the {policyType.GetName()} policy.");
        }
    }

    private void ValidateEnablingRequirements(
        PolicyType policyType,
        Dictionary<PolicyType, Policy> savedPoliciesDict)
    {
        var result = policyEventHandlerFactory.GetHandler<IEnforceDependentPoliciesEvent>(policyType);

        result.Switch(
            validator =>
            {
                var missingRequiredPolicyTypes = validator.RequiredPolicies
                    .Where(requiredPolicyType => savedPoliciesDict.GetValueOrDefault(requiredPolicyType) is not { Enabled: true })
                    .ToList();

                if (missingRequiredPolicyTypes.Count != 0)
                {
                    throw new BadRequestException($"Turn on the {missingRequiredPolicyTypes.First().GetName()} policy because it is required for the {policyType.GetName()} policy.");
                }
            },
            _ => { /* Policy has no required dependencies */ });
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

    Task PushPolicyUpdateToClients(Guid organizationId, Policy policy) => pushNotificationService.PushAsync(new PushNotification<SyncPolicyPushNotification>
    {
        Type = PushType.PolicyChanged,
        Target = NotificationTarget.Organization,
        TargetId = organizationId,
        ExcludeCurrentContext = false,
        Payload = new SyncPolicyPushNotification
        {
            Policy = policy,
            OrganizationId = organizationId
        }
    });
}
