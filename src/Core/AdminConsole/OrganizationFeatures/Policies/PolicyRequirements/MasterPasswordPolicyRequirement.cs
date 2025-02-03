using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public class MasterPasswordPolicyRequirement : MasterPasswordPolicyData, IPolicyRequirement
{
    public static MasterPasswordPolicyRequirement Create(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails) =>
        userPolicyDetails
            .GetPolicyType(PolicyType.MasterPassword)
            .ExcludeProviders()
            .ExcludeRevokedAndInvitedUsers()
            .Select(up => up.GetDataModel<MasterPasswordPolicyData>())
            .Aggregate(
                new MasterPasswordPolicyRequirement(),
                (result, current) =>
                {
                    result.CombineWith(current);
                    return result;
                }
            );
}
