#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public class OrganizationDataOwnershipPolicyValidator(
    IPolicyRepository policyRepository,
    ICollectionRepository collectionRepository,
    IEnumerable<IPolicyRequirementFactory<IPolicyRequirement>> factories)
    : OrganizationPolicyValidator(policyRepository, factories)
{
    private readonly ICollectionRepository _collectionRepository = collectionRepository;
    public override PolicyType Type => PolicyType.OrganizationDataOwnership;

    public override IEnumerable<PolicyType> RequiredPolicies => [];

    public override Task<string> ValidateAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        // Logic: Validate anything needed for this policy enabling or disabling.

        return Task.FromResult("");
    }

    public override async Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {

        if (currentPolicy is not { Enabled: true } && policyUpdate is { Enabled: true })
        {
            await UpsertDefaultCollectionsForUsersAsync(policyUpdate);
        }

    }

    private async Task UpsertDefaultCollectionsForUsersAsync(PolicyUpdate policyUpdate)
    {
        var requirements = await GetUserPolicyRequirementsByOrganizationIdAsync<OrganizationDataOwnershipPolicyRequirement>(policyUpdate.OrganizationId, policyUpdate.Type);

        await _collectionRepository.UpsertDefaultCollectionsAsync(
            policyUpdate.OrganizationId,
            GetUserOrgIds(policyUpdate, requirements),
            GetDefaultUserCollectionName());
    }

    private static string GetDefaultUserCollectionName()
    {
        return "Default";
    }

    private static List<Guid> GetUserOrgIds(PolicyUpdate policyUpdate, IEnumerable<OrganizationDataOwnershipPolicyRequirement> requirements)
    {
        var userOrgIds = new List<Guid>();
        foreach (var requirement in requirements)
        {
            var userOrgId = requirement.GetOrganizationUserId(policyUpdate.OrganizationId);

            if (userOrgId.HasValue)
            {
                userOrgIds.Add(userOrgId.Value);
            }
        }
        return userOrgIds;
    }
}
