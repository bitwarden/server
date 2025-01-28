using Bit.Core.AdminConsole.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public static class PolicyRequirementHelpers
{
    public static IEnumerable<OrganizationUserPolicyDetails> GetPolicyType(
        this IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails,
        PolicyType type) =>
            userPolicyDetails.Where(x => x.PolicyType == type);

    public static IEnumerable<OrganizationUserPolicyDetails> ExcludeOwnersAndAdmins(
        this IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails) =>
            userPolicyDetails.Where(x => x.OrganizationUserType != OrganizationUserType.Owner);

    public static IEnumerable<OrganizationUserPolicyDetails> ExcludeProviders(
        this IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails) =>
            userPolicyDetails.Where(x => !x.IsProvider);

    public static IEnumerable<OrganizationUserPolicyDetails> ExcludeRevokedAndInvitedUsers(
        this IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails) =>
            userPolicyDetails.Where(x => x.OrganizationUserStatus >= OrganizationUserStatusType.Accepted);

    public static IEnumerable<OrganizationUserPolicyDetails> ExcludeRevokedUsers(
        this IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails) =>
            userPolicyDetails.Where(x => x.OrganizationUserStatus >= OrganizationUserStatusType.Invited);
}
