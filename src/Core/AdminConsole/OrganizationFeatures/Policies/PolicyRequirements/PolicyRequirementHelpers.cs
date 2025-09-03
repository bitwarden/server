using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public static class PolicyRequirementHelpers
{
    /// <summary>
    /// Returns true if the <see cref="PolicyDetails"/> is for one of the specified roles, false otherwise.
    /// </summary>
    public static bool HasRole(
        this PolicyDetails policyDetails,
        IEnumerable<OrganizationUserType> roles)
        => roles.Contains(policyDetails.OrganizationUserType);

    /// <summary>
    /// Returns true if the <see cref="PolicyDetails"/> relates to one of the specified statuses, false otherwise.
    /// </summary>
    public static bool HasStatus(this PolicyDetails policyDetails, IEnumerable<OrganizationUserStatusType> status)
        => status.Contains(policyDetails.OrganizationUserStatus);
}
