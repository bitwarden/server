
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public class OrganizationDataOwnershipPolicyValidator(
    IPolicyRepository policyRepository,
    ICollectionRepository collectionRepository,
    IOrganizationRepository organizationRepository,
    IEnumerable<IPolicyRequirementFactory<IPolicyRequirement>> factories)
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
        // FIXME: we should use the organizationAbility cache here, but it is currently flaky
        // and it's not obvious how to handle a cache failure.
        // https://bitwarden.atlassian.net/browse/PM-32699
        var organization = await organizationRepository.GetByIdAsync(policyUpdate.OrganizationId);
        if (organization == null)
        {
            throw new InvalidOperationException($"Organization with ID {policyUpdate.OrganizationId} not found.");
        }

        if (!organization.UseMyItems)
        {
            return;
        }

        var requirements = await GetUserPolicyRequirementsByOrganizationIdAsync<OrganizationDataOwnershipPolicyRequirement>(policyUpdate.OrganizationId, policyUpdate.Type);

        var userOrgIds = requirements
            .Select(requirement => requirement.GetDefaultCollectionRequestOnPolicyEnable(policyUpdate.OrganizationId))
            .Where(request => request.ShouldCreateDefaultCollection)
            .Select(request => request.OrganizationUserId)
            .ToList();

        if (!userOrgIds.Any())
        {
            return;
        }

        await collectionRepository.CreateDefaultCollectionsBulkAsync(
            policyUpdate.OrganizationId,
            userOrgIds,
            defaultCollectionName);
    }
}
