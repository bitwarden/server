using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface IPolicyRequirement
{
    /// <summary>
    /// A transitional method used to map the policy requirement to the deprecated IPolicyService.AnyPoliciesApplicableToUser.
    /// </summary>
    bool AppliesToUser { get; }
};

public interface IPolicyRequirementDefinition<T> where T : IPolicyRequirement
{
    /// <summary>
    /// The PolicyType that this requirement applies to.
    /// </summary>
    PolicyType Type { get; }

    /// <summary>
    /// A reducer that takes an input of policy details and returns a single IPolicyRequirement which summarizes the
    /// restrictions that should be enforced against the user. This is used by domain code to enforce the policy.
    /// </summary>
    /// <param name="userPolicyDetails">A DTO representing an organization user and the relevant policy for that organization.</param>
    /// <returns></returns>
    T Reduce(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails);

    /// <summary>
    /// A predicate that returns true if the policy should be enforced against the user, false otherwise.
    /// The implementation may assume that it will only receive enabled policies for organizations whose plan supports policies.
    /// </summary>
    /// <remarks>
    /// For example, you may not want to enforce a policy against certain roles (e.g. providers, owners or admins)
    /// or against users with a certain status (e.g. invited or revoked users). This is your responsibility to define.
    /// </remarks>
    /// <param name="userPolicyDetails">A DTO representing an organization user and the relevant policy for that organization.</param>
    /// <returns>A boolean used to filter a sequence of policies.</returns>
    bool FilterPredicate(OrganizationUserPolicyDetails userPolicyDetails);
}


