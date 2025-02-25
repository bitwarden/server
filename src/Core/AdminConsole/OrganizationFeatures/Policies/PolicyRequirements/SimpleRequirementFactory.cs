using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public abstract class SimpleRequirementFactory<T> : IRequirementFactory<T> where T : IPolicyRequirement
{
    protected abstract IEnumerable<OrganizationUserType> ExemptRoles { get; }

    public abstract IEnumerable<PolicyType> PolicyTypes { get; }

    public IEnumerable<PolicyDetails> Filter(IEnumerable<PolicyDetails> policyDetails)
        => policyDetails
            .ExemptRoles(ExemptRoles)
            .ExemptStatus([OrganizationUserStatusType.Invited, OrganizationUserStatusType.Revoked])
            .ExemptProviders();

    public abstract T Create(IEnumerable<PolicyDetails> policyDetails);
}
