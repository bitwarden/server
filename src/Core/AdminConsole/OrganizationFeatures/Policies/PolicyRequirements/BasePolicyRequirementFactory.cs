using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// A simple base implementation of <see cref="IPolicyRequirementFactory{T}"/> which will be suitable for most policies.
/// It provides sensible defaults to help teams to implement their own Policy Requirements.
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class BasePolicyRequirementFactory<T> : IPolicyRequirementFactory<T> where T : IPolicyRequirement
{
    /// <summary>
    /// User roles that are exempt from policy enforcement.
    /// Owners and Admins are exempt by default but this may be overridden.
    /// </summary>
    protected virtual IEnumerable<OrganizationUserType> ExemptRoles { get; } =
        [OrganizationUserType.Owner, OrganizationUserType.Admin];

    /// <summary>
    /// User statuses that are exempt from policy enforcement.
    /// Invited and Revoked users are exempt by default, which is appropriate in the majority of cases.
    /// </summary>
    protected virtual IEnumerable<OrganizationUserStatusType> ExemptStatuses { get; } =
        [OrganizationUserStatusType.Invited, OrganizationUserStatusType.Revoked];

    /// <summary>
    /// Whether a Provider User for the organization is exempt from policy enforcement.
    /// Provider Users are exempt by default, which is appropriate in the majority of cases.
    /// </summary>
    protected virtual bool ExemptProviders { get; } = true;

    /// <inheritdoc />
    public abstract PolicyType PolicyType { get; }

    public bool Enforce(PolicyDetails policyDetails)
        => !policyDetails.HasRole(ExemptRoles) &&
            !policyDetails.HasStatus(ExemptStatuses) &&
            (!policyDetails.IsProvider || !ExemptProviders);

    /// <inheritdoc />
    public abstract T Create(IEnumerable<PolicyDetails> policyDetails);
}
