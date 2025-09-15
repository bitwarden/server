
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

/// <summary>
/// Please do not extend or expand this validator. We're currently in the process of refactoring our policy validator pattern.
/// This is a stop-gap solution for post-policy-save side effects, but it is not the long-term solution.
/// </summary>
public class OrganizationDataOwnershipPolicyHandler(
    IPolicyRepository policyRepository,
    ICollectionRepository collectionRepository,
    IEnumerable<IPolicyRequirementFactory<IPolicyRequirement>> factories,
    IFeatureService featureService)
    : OrganizationPolicyHandler(policyRepository, factories), IPostSavePolicySideEffect, IOnPolicyPostSaveEvent
{
    [Obsolete("Use ExecuteSideEffectsAsync instead", true)]
    public async Task ExecuteSideEffectsAsync(
        SavePolicyModel policyRequest,
        Policy postUpdatedPolicy,
        Policy? previousPolicyState)
    {
       await ExecutePostUpsertSideEffectAsync(policyRequest, postUpdatedPolicy, previousPolicyState);
    }

    public async Task ExecutePostUpsertSideEffectAsync(
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

    public PolicyType Type => PolicyType.OrganizationDataOwnership;

}
