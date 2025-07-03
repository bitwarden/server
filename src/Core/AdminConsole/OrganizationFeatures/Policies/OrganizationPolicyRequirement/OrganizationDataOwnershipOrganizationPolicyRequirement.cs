#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;

using Bit.Core.AdminConsole.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;



public abstract class OrganizationPolicyValidator(IPolicyRepository policyRepository) : IPolicyValidator
{
    public abstract PolicyType Type { get; }

    public abstract IEnumerable<PolicyType> RequiredPolicies { get; }

    public async Task<OrganizationPolicyDetails> GetOrganizationPolicyRequirement(PolicyUpdate policyUpdate)
    {

        // Jimmy break this up into 2 calls for policy and users
        var baseResult = await policyRepository.GetOrganizationPolicyDetailsByOrgId(policyUpdate.OrganizationId, policyUpdate.Type);

        return FilterRequirementAsync(baseResult, policyUpdate);
    }

    protected virtual OrganizationPolicyDetails FilterRequirementAsync(OrganizationPolicyDetails baseResult, PolicyUpdate policyUpdate)
    {
        return baseResult;
    }

    public abstract Task OnSaveSideEffectsAsync(
        PolicyUpdate policyUpdate,
        Policy? currentPolicy
    );

    public abstract Task<string> ValidateAsync(
        PolicyUpdate policyUpdate,
        Policy? currentPolicy
    );
}

public class OrganizationDataOwnershipPolicyValidator : OrganizationPolicyValidator
{
    public override PolicyType Type => PolicyType.OrganizationDataOwnership;

    public override IEnumerable<PolicyType> RequiredPolicies => [];

    public OrganizationDataOwnershipPolicyValidator(IPolicyRepository policyRepository) : base(policyRepository)
    {

    }

    public override Task<string> ValidateAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        // Logic: Validate anything needed for this policy enabling or disabling.

        return Task.FromResult("");
    }

    public override async Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        var affectUsers = await GetOrganizationPolicyRequirement(policyUpdate);

    }

    // Jimmy basically turn FilterRequirementAsync into a generic similar to the policyReq create method

    protected override OrganizationPolicyDetails FilterRequirementAsync(OrganizationPolicyDetails baseResult, PolicyUpdate policyUpdate)
    {
        // Individual policy can filter it.
        var results = baseResult.Users.Where(user => user.OrganizationUserStatus == Core.Enums.OrganizationUserStatusType.Confirmed);

        return baseResult;
    }


}

