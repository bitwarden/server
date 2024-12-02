using Bit.Core.AdminConsole.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

/// <summary>
/// Represents the business rules of a specified <see cref="PolicyType"/> and how they should be enforced against a user.
/// </summary>
public interface IPolicyRequirement;

/// <summary>
/// An interface that defines how a set of organization policies are transformed to a single <see cref="IPolicyRequirement"/>.
/// This must be implemented for any <see cref="PolicyType"/> that is enforced on the server.
/// </summary>
/// <typeparam name="T">The <see cref="IPolicyRequirement"/> that the class produces.</typeparam>
public interface IPolicyRequirementFactory<out T> where T : IPolicyRequirement
{
    /// <summary>
    /// The PolicyType that this class applies to.
    /// </summary>
    PolicyType Type { get; }

    /// <summary>
    /// A reducer that takes an input of policy details and returns a single IPolicyRequirement.
    /// </summary>
    /// <param name="userPolicyDetails">A DTO representing an organization user and the relevant policy for that organization.</param>
    T CreateRequirement(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails);

    /// <summary>
    /// A predicate that returns true if the policy should be enforced against the user, false otherwise.
    /// The implementation may assume that it will only receive enabled policies for organizations whose plan supports policies.
    /// </summary>
    /// <remarks>
    /// For example, you may not want to enforce a policy against certain roles (e.g. providers, owners or admins)
    /// or against users with a certain status (e.g. invited or revoked users). This is your responsibility to define.
    /// </remarks>
    /// <param name="userPolicyDetails">A DTO representing an organization user and the relevant policy for that organization.</param>
    /// <returns>True if the policy should be enforced against the user or false otherwise.</returns>
    bool EnforcePolicy(OrganizationUserPolicyDetails userPolicyDetails);
}


