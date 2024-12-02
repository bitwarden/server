using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Extensions;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public record DisableSendPolicyRequirementFactory : IPolicyRequirementFactory<DisableSendPolicyRequirement>
{
    public PolicyType Type => PolicyType.DisableSend;

    public DisableSendPolicyRequirement CreateRequirement(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails) =>
        new(userPolicyDetails.Any());

    public bool EnforcePolicy(OrganizationUserPolicyDetails userPolicyDetails) =>
        userPolicyDetails.OrganizationUserStatus > OrganizationUserStatusType.Invited &&
        !userPolicyDetails.IsAdminType();
}

public record DisableSendPolicyRequirement(bool DisableSend) : IPolicyRequirement;
