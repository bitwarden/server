#nullable enable

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

    public override async Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.CreateDefaultLocation))
        {
            return;
        }

        if (currentPolicy?.Enabled != true && policyUpdate.Enabled)
        {
            await UpsertDefaultCollectionsForUsersAsync(policyUpdate);
        }
    }

    private async Task UpsertDefaultCollectionsForUsersAsync(PolicyUpdate policyUpdate)
    {
        var requirements = await GetUserPolicyRequirementsByOrganizationIdAsync<OrganizationDataOwnershipPolicyRequirement>(policyUpdate.OrganizationId, policyUpdate.Type);

        var userOrgIds = requirements
            .Select(requirement => requirement.GetDefaultCollectionRequestOnPolicyEnable(policyUpdate.OrganizationId))
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
            GetDefaultUserCollectionName());
    }

    private static string GetDefaultUserCollectionName()
    {
        // TODO: https://bitwarden.atlassian.net/browse/PM-24279
        const string temporaryPlaceHolderValue = "Default";
        return temporaryPlaceHolderValue;
    }
}
