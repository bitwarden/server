#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;

using Bit.Core.AdminConsole.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;


/* prototype comment: 

 1. Individual policies that need to retrieve the bulk-affected users can implement this class.
 2. They can override FilterPolicyUsers to fit their needs.
 3. All child classes can integrate into the existing IPolicyValidator seamlessly. 
*/

public abstract class OrganizationPolicyValidator(IPolicyRepository policyRepository) : IPolicyValidator
{
    public abstract PolicyType Type { get; }

    public abstract IEnumerable<PolicyType> RequiredPolicies { get; }


    public async Task<IEnumerable<UserPolicyDetails>> GetOrganizationPolicyRequirement(PolicyUpdate policyUpdate)
    {
        // Question: do we need the policy data here since we already have them?
        var baseResult = await policyRepository.GetOrganizationPolicyDetailsByOrgId(policyUpdate.OrganizationId, policyUpdate.Type);

        return FilterPolicyUsers(baseResult, policyUpdate);
    }

    // Question: Does this really need to be generic? We only want it to be called in the validator, 
    // and the validator already knows the type.

    // Question: We could use the exclude properties like PolicyRequirementQuery, 
    // but we're not getting back all user statuses. So we could add something similar here, 
    // but it's not quite the same.

    // Note: I noticed that we're trying really hard to make IPolicyRequirement work here, 
    // but the pattern doesn't translate well because the original constraints no longer exist. 
    // We shouldn't keep the added complexity without a good reason.

    protected virtual IEnumerable<UserPolicyDetails> FilterPolicyUsers(IEnumerable<UserPolicyDetails> baseUserResults, PolicyUpdate policyUpdate)
    {
        return baseUserResults;
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


// prototype comment: first example with the policy we want
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


    protected override IEnumerable<UserPolicyDetails> FilterPolicyUsers(IEnumerable<UserPolicyDetails> baseUserResults, PolicyUpdate policyUpdate)
    {
        // Individual policy can filter it.
        var results = baseUserResults.Where(user => user.OrganizationUserStatus == Core.Enums.OrganizationUserStatusType.Confirmed);

        return results;
    }


}

// prototype comment: Just a second example of how this will look like 
public class PasswordGeneratorPolicyValidator : OrganizationPolicyValidator
{
    public override PolicyType Type => PolicyType.PasswordGenerator;

    public override IEnumerable<PolicyType> RequiredPolicies => [];

    public PasswordGeneratorPolicyValidator(IPolicyRepository policyRepository) : base(policyRepository)
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
        // logic: do something with the side effect users. 

    }

    protected override IEnumerable<UserPolicyDetails> FilterPolicyUsers(IEnumerable<UserPolicyDetails> baseUserResults, PolicyUpdate policyUpdate)
    {
        // Individual policy can filter it.
        var results = baseUserResults.Where(user => user.OrganizationUserStatus == Core.Enums.OrganizationUserStatusType.Confirmed);

        return results;
    }


}

