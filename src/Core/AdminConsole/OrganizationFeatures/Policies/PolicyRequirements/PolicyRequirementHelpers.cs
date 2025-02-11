using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public static class PolicyRequirementHelpers
{
    public static IEnumerable<PolicyDetails> GetPolicyType(
        this IEnumerable<PolicyDetails> userPolicyDetails,
        PolicyType type) =>
            userPolicyDetails.Where(x => x.PolicyType == type);

    public static IEnumerable<PolicyDetails> ExcludeOwnersAndAdmins(
        this IEnumerable<PolicyDetails> userPolicyDetails) =>
            userPolicyDetails.Where(x => x.OrganizationUserType is not OrganizationUserType.Owner and not OrganizationUserType.Admin);

    public static IEnumerable<PolicyDetails> ExcludeProviders(
        this IEnumerable<PolicyDetails> userPolicyDetails) =>
            userPolicyDetails.Where(x => !x.IsProvider);

    public static IEnumerable<PolicyDetails> ExcludeRevokedAndInvitedUsers(
        this IEnumerable<PolicyDetails> userPolicyDetails) =>
            userPolicyDetails.Where(x => x.OrganizationUserStatus >= OrganizationUserStatusType.Accepted);

    public static IEnumerable<PolicyDetails> ExcludeRevokedUsers(
        this IEnumerable<PolicyDetails> userPolicyDetails) =>
            userPolicyDetails.Where(x => x.OrganizationUserStatus >= OrganizationUserStatusType.Invited);
}
