using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Extensions;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public record PersonalOwnershipPolicyRequirementDefinition : IPolicyRequirementDefinition<PersonalOwnershipPolicyRequirement>
{
    public PolicyType Type => PolicyType.PersonalOwnership;

    public PersonalOwnershipPolicyRequirement Reduce(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails) =>
        new(userPolicyDetails.Any());

    public bool FilterPredicate(OrganizationUserPolicyDetails userPolicyDetails) =>
        userPolicyDetails.OrganizationUserStatus > OrganizationUserStatusType.Invited &&
        !userPolicyDetails.IsAdminType();
}

public record PersonalOwnershipPolicyRequirement(bool DisablePersonalOwnership) : IPolicyRequirement
{
    public bool AppliesToUser => DisablePersonalOwnership;
};

