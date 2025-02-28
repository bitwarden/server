using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public static class PolicyRequirementHelpers
{
    /// <summary>
    /// Filters the PolicyDetails to remove the specified user roles. This can be used to exempt
    /// owners and admins from policy enforcement.
    /// </summary>
    public static bool HasRole(
        this PolicyDetails policyDetails,
        IEnumerable<OrganizationUserType> roles)
        => roles.Contains(policyDetails.OrganizationUserType);

    /// <summary>
    /// Filters the PolicyDetails to remove the specified organization user statuses. For example, this can be used
    /// to exempt users in the invited and revoked statuses from policy enforcement.
    /// </summary>
    public static bool HasStatus(this PolicyDetails policyDetails, IEnumerable<OrganizationUserStatusType> status)
        => !status.Contains(policyDetails.OrganizationUserStatus);
}
