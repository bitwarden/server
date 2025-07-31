using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public interface IPolicyRequirementFactory
{
    /// <summary>
    /// The PolicyType this factory is for.
    /// </summary>
    PolicyType PolicyType { get; }

    /// <summary>
    /// Return true if the role is exempt from the policy.
    /// </summary>
    /// <returns></returns>
    bool ExemptRoles(OrganizationUserType role);

    /// <summary>
    /// True if providers are exempt from the policy.
    /// </summary>
    bool ExemptProviders { get; }

    /// <summary>
    /// If true, the policy will be enforced against users in the accepted status.
    /// If false, the policy will not be enforced against accepted users; the user must be confirmed before they are
    /// affected by the policy.
    /// </summary>
    bool EnforceInAcceptedStatus { get; }
}

public interface ISinglePolicyRequirementFactory<out T> : IPolicyRequirementFactory where T : ISinglePolicyRequirement
{
    T Create(PolicyDetails? policyDetails = null);
}

public interface IAggregatePolicyRequirementFactory<out T> : IPolicyRequirementFactory where T : IAggregatePolicyRequirement
{
    T Create(IEnumerable<PolicyDetails> policyDetails);
}
