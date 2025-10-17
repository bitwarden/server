﻿
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public class OrganizationDataOwnershipPolicyValidator(
    IPolicyRepository policyRepository,
    ICollectionRepository collectionRepository,
    IEnumerable<IPolicyRequirementFactory<IPolicyRequirement>> factories,
    IFeatureService featureService)
    : OrganizationPolicyValidator(policyRepository, factories), IPostSavePolicySideEffect, IOnPolicyPostUpdateEvent
{
    public PolicyType Type => PolicyType.OrganizationDataOwnership;

    public async Task ExecutePostUpsertSideEffectAsync(
        SavePolicyModel policyRequest,
        Policy postUpsertedPolicyState,
        Policy? previousPolicyState)
    {
        await ExecuteSideEffectsAsync(policyRequest, postUpsertedPolicyState, previousPolicyState);
    }

    public async Task ExecuteSideEffectsAsync(
        SavePolicyModel policyRequest,
        Policy postUpdatedPolicy,
        Policy? previousPolicyState)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.CreateDefaultLocation))
        {
            return;
        }

        if (policyRequest.Metadata is not OrganizationModelOwnershipPolicyModel metadata)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(metadata.DefaultUserCollectionName))
        {
            return;
        }

        var isFirstTimeEnabled = postUpdatedPolicy.Enabled && previousPolicyState == null;
        var reEnabled = previousPolicyState?.Enabled == false
                        && postUpdatedPolicy.Enabled;

        if (isFirstTimeEnabled || reEnabled)
        {
            await UpsertDefaultCollectionsForUsersAsync(policyRequest.PolicyUpdate, metadata.DefaultUserCollectionName);
        }
    }

    private async Task UpsertDefaultCollectionsForUsersAsync(PolicyUpdate policyUpdate, string defaultCollectionName)
    {
        var requirements = await GetUserPolicyRequirementsByOrganizationIdAsync<OrganizationDataOwnershipPolicyRequirement>(policyUpdate.OrganizationId, policyUpdate.Type);

        var userOrgIds = requirements
            .Select(requirement => requirement.GetDefaultCollectionRequestOnPolicyEnable(policyUpdate.OrganizationId))
            .Where(request => request.ShouldCreateDefaultCollection)
            .Select(request => request.OrganizationUserId);

        if (!userOrgIds.Any())
        {
            return;
        }

        await collectionRepository.UpsertDefaultCollectionsAsync(
            policyUpdate.OrganizationId,
            userOrgIds,
            defaultCollectionName);
    }
}
