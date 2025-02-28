using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// A simple base implementation of <see cref="IRequirementFactory{T}"/> which will be suitable for most policies.
/// It automatically excludes organization members in the Invited and Revoked status, as well as Provider Users.
/// The implementation is only required to specify exempt roles (if any) and implement the Create method.
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class SimpleRequirementFactory<T> : IRequirementFactory<T> where T : IPolicyRequirement
{
    protected abstract IEnumerable<OrganizationUserType> ExemptRoles { get; }

    public abstract PolicyType PolicyType { get; }

    public bool Enforce(PolicyDetails policyDetails)
        => !policyDetails.HasRole(ExemptRoles) &&
            !policyDetails.HasStatus([OrganizationUserStatusType.Invited, OrganizationUserStatusType.Revoked]) &&
            !policyDetails.IsProvider;

    public abstract T Create(IEnumerable<PolicyDetails> policyDetails);
}
