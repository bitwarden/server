using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// An object that represents how a <see cref="PolicyType"/> will be enforced against a user.
/// This acts as a bridge between the <see cref="Policy"/> entity saved to the database and the domain that the policy
/// affects. You may represent the impact of the policy in any way that makes sense for the domain.
/// </summary>
public interface IPolicyRequirement;

// my test
public interface ISinglePolicyRequirement : IPolicyRequirement //<out T> where T : ISinglePolicyRequirement<T>
{
    // static abstract T Create(PolicyDetails policyDetails);
}

public interface IAggregatePolicyRequirement : IPolicyRequirement //<out T> where T : IAggregatePolicyRequirement<T>
{
    // static abstract T Create(IEnumerable<PolicyDetails> policyDetails);
}

// decided not to put create method on the object b/c then you can't link it to its definition unless you
// put the generic type on the definition, or the policyType on the object, and in both cases you may as well
// then put it all in the def and save the hassle
