using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Extensions;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public record MasterPasswordPolicyRequirementFactory : IPolicyRequirementFactory<MasterPasswordPolicyRequirement>
{
    public PolicyType Type => PolicyType.MasterPassword;

    public MasterPasswordPolicyRequirement CreateRequirement(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails) =>
        userPolicyDetails
            .Select(up => up.GetDataModel<MasterPasswordPolicyData>())
            .Aggregate(
                new MasterPasswordPolicyRequirement(),
                (result, current) =>
                {
                    result.CombineWith(current);
                    return result;
                }
            );

    public bool EnforcePolicy(OrganizationUserPolicyDetails userPolicyDetails) =>
        userPolicyDetails.OrganizationUserStatus > OrganizationUserStatusType.Invited;
}

public class MasterPasswordPolicyRequirement : MasterPasswordPolicyData, IPolicyRequirement;
