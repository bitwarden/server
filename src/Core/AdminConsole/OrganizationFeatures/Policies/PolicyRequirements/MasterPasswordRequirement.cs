using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirementQueries;

public class MasterPasswordRequirement : MasterPasswordPolicyData
{
    public static MasterPasswordRequirement Create(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails) =>
        userPolicyDetails
            .GetPolicyType(PolicyType.MasterPassword)
            .ExcludeProviders()
            .ExcludeRevokedAndInvitedUsers()
            .Select(up => up.GetDataModel<MasterPasswordPolicyData>())
            .Aggregate(
                new MasterPasswordRequirement(),
                (result, current) =>
                {
                    result.CombineWith(current);
                    return result;
                }
            );
}
