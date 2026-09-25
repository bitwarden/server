using Bit.Core.AdminConsole.Enums;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;

/// <summary>
/// Evaluates a target organization's policies against users who are about to be granted access to it
/// (e.g. accept, confirm and restore flows). This is "what if" enforcement: the user may not have an
/// OrganizationUser record yet, so the caller supplies the role the user would hold.
/// </summary>
/// <remarks>
/// Obtain an instance from <see cref="IPreAccessEnforcerQuery"/>. All organization state is loaded up front,
/// so <see cref="Evaluate"/> can be called for any number of policies and users without further database calls.
/// This only enforces the target organization's policies; use <see cref="IPolicyRequirementQuery"/> to enforce
/// policies from organizations the user already belongs to.
/// </remarks>
public interface IPreAccessPolicyEnforcer
{
    /// <summary>
    /// Evaluates whether a policy of the target organization must be enforced against the user.
    /// Per-policy exemptions (roles, providers) are taken from the registered policy requirement factories.
    /// </summary>
    /// <param name="policyType">The policy to evaluate.</param>
    /// <param name="userId">The user who is about to be granted access.</param>
    /// <param name="proposedRole">The role the user will hold in the target organization.</param>
    PreAccessPolicyResult Evaluate(PolicyType policyType, Guid userId, OrganizationUserType proposedRole);
}
