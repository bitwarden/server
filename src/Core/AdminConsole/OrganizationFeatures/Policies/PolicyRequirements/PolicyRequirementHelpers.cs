using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public static class PolicyRequirementHelpers
{
    /// <summary>
    /// Filters the PolicyDetails by PolicyType. This is generally required to only get the PolicyDetails that your
    /// IPolicyRequirement relates to.
    /// </summary>
    public static IEnumerable<PolicyDetails> GetPolicyType(
        this IEnumerable<PolicyDetails> policyDetails,
        PolicyType type)
        => policyDetails.Where(x => x.PolicyType == type);

    /// <summary>
    /// Filters the PolicyDetails to remove the specified user roles. This can be used to exempt
    /// owners and admins from policy enforcement.
    /// </summary>
    public static IEnumerable<PolicyDetails> ExemptRoles(
        this IEnumerable<PolicyDetails> policyDetails,
        IEnumerable<OrganizationUserType> roles)
        => policyDetails.Where(x => !roles.Contains(x.OrganizationUserType));

    /// <summary>
    /// Filters the PolicyDetails to remove organization users who are also provider users for the organization.
    /// This can be used to exempt provider users from policy enforcement.
    /// </summary>
    public static IEnumerable<PolicyDetails> ExemptProviders(this IEnumerable<PolicyDetails> policyDetails)
        => policyDetails.Where(x => !x.IsProvider);

    /// <summary>
    /// Filters the PolicyDetails to remove the specified organization user statuses. For example, this can be used
    /// to exempt users in the invited and revoked statuses from policy enforcement.
    /// </summary>
    public static IEnumerable<PolicyDetails> ExemptStatus(
        this IEnumerable<PolicyDetails> policyDetails, IEnumerable<OrganizationUserStatusType> status)
        => policyDetails.Where(x => !status.Contains(x.OrganizationUserStatus));
}
