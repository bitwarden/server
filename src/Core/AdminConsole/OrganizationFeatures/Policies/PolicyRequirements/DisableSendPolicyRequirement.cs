using Bit.Core.AdminConsole.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public record DisableSendPolicyRequirement(bool DisableSend) : IPolicyRequirement
{
    public bool AppliesToUser => DisableSend;
};

public record DisableSendPolicyRequirementDefinition : IPolicyRequirementDefinition<DisableSendPolicyRequirement>
{
    public PolicyType Type => PolicyType.DisableSend;

    public DisableSendPolicyRequirement Reduce(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails) =>
        new(userPolicyDetails.Any());

    public bool FilterPredicate(OrganizationUserPolicyDetails userPolicyDetails) =>
        userPolicyDetails.OrganizationUserStatus > OrganizationUserStatusType.Invited &&
        userPolicyDetails.OrganizationUserType is not (OrganizationUserType.Owner or OrganizationUserType.Admin) &&
        !userPolicyDetails.IsProvider;
}
