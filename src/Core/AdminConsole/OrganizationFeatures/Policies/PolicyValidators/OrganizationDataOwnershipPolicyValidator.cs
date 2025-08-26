
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public class OrganizationDataOwnershipPolicyValidator(
    IPolicyRepository policyRepository,
    ICollectionRepository collectionRepository,
    IEnumerable<IPolicyRequirementFactory<IPolicyRequirement>> factories,
    IFeatureService featureService,
    ILogger<OrganizationDataOwnershipPolicyValidator> logger)
    : OrganizationPolicyValidator(policyRepository, factories)
{
    public override PolicyType Type => PolicyType.OrganizationDataOwnership;

    public override IEnumerable<PolicyType> RequiredPolicies => [];

    public override Task<string> ValidateAsync(PolicyUpdate policyUpdate, Policy? currentPolicy) => Task.FromResult("");

    public override async Task OnSaveSideEffectsAsync(SavePolicyModel policyModel, Policy? currentPolicy)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.CreateDefaultLocation))
        {
            return;
        }

        if (policyModel.Metadata is not OrganizationModelOwnershipPolicyModel metadata)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(metadata.DefaultUserCollectionName))
        {
            return;
        }

        var policyUpdate = policyModel.PolicyUpdate;

        if (currentPolicy?.Enabled == true || !policyUpdate.Enabled)
        {
            return;
        }

        await UpsertDefaultCollectionsForUsersAsync(policyUpdate, metadata.DefaultUserCollectionName);
    }

    public override Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy) => Task.FromResult(0);


    private async Task UpsertDefaultCollectionsForUsersAsync(PolicyUpdate policyUpdate, string defaultCollectionName)
    {
        var requirements = await GetUserPolicyRequirementsByOrganizationIdAsync<OrganizationDataOwnershipPolicyRequirement>(policyUpdate.OrganizationId, policyUpdate.Type);

        var userOrgIds = requirements
            .Select(requirement => requirement.GetDefaultCollectionRequest(policyUpdate.OrganizationId))
            .Where(request => request.ShouldCreateDefaultCollection)
            .Select(request => request.OrganizationUserId);

        if (!userOrgIds.Any())
        {
            logger.LogError("No UserOrganizationIds found for {OrganizationId}", policyUpdate.OrganizationId);
            return;
        }

        await collectionRepository.UpsertDefaultCollectionsAsync(
            policyUpdate.OrganizationId,
            userOrgIds,
            defaultCollectionName);
    }

}
