using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Extensions;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public record DisableSendPolicyRequirementDefinition : IPolicyRequirementDefinition<DisableSendPolicyRequirement>
{
    public PolicyType Type => PolicyType.DisableSend;

    public DisableSendPolicyRequirement Reduce(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails) =>
        new(userPolicyDetails.Any());

    public bool FilterPredicate(OrganizationUserPolicyDetails userPolicyDetails) =>
        userPolicyDetails.OrganizationUserStatus > OrganizationUserStatusType.Invited &&
        !userPolicyDetails.IsAdminType();
}

public record DisableSendPolicyRequirement(bool DisableSend) : IPolicyRequirement;
