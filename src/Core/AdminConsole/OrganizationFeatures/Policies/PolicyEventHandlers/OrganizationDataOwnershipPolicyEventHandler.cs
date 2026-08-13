using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyEventHandlers;

public class OrganizationDataOwnershipPolicyEventHandler(
    IPolicyRepository policyRepository,
    ICollectionRepository collectionRepository,
    IOrganizationAbilityCacheService organizationAbilityCacheService,
    IEnumerable<IPolicyRequirementFactory<IPolicyRequirement>> factories)
    : OrganizationPolicyEventHandler(policyRepository, factories), IOnPolicyPostUpdateEvent
{
    public PolicyType Type => PolicyType.OrganizationDataOwnership;

    public async Task ExecutePostUpsertSideEffectAsync(
        SavePolicyModel policyRequest,
        Policy postUpsertedPolicyState,
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

        var isFirstTimeEnabled = postUpsertedPolicyState.Enabled && previousPolicyState == null;
        var reEnabled = previousPolicyState?.Enabled == false
                        && postUpsertedPolicyState.Enabled;

        if (isFirstTimeEnabled || reEnabled)
        {
            await UpsertDefaultCollectionsForUsersAsync(policyRequest.PolicyUpdate, metadata.DefaultUserCollectionName);
        }
    }

    private async Task UpsertDefaultCollectionsForUsersAsync(PolicyUpdate policyUpdate, string defaultCollectionName)
    {
        if (!await OrganizationUsesMyItemsAsync(policyUpdate.OrganizationId))
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

    private async Task<bool> OrganizationUsesMyItemsAsync(Guid organizationId)
    {
        var organizationAbility = await organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId);
        if (organizationAbility == null)
        {
            throw new NotFoundException($"Organization with ID {organizationId} not found.");
        }

        return organizationAbility.UseMyItems;
    }
}
