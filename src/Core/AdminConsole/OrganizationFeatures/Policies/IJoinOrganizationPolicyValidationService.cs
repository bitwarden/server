using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

/// <summary>
/// Reusable policy checks for a user joining an organization, shared by the validators of the flows that add a user
/// to an organization.
/// </summary>
public interface IJoinOrganizationPolicyValidationService
{
    /// <summary>
    /// Checks the policies that gate a user joining an organization, in this order:
    /// <list type="bullet">
    ///     <item>Single Organization: the user's other organizations, then the organization being joined.</item>
    ///     <item>Require Two-Factor Authentication: the organization being joined.</item>
    ///     <item>Automatic User Confirmation: the user's other organizations, then the organization being joined.</item>
    /// </list>
    /// The user's other organizations are evaluated through <see cref="IPolicyRequirementQuery"/>. The organization
    /// being joined is evaluated through <see cref="PreAccess.IPreAccessPolicyEnforcer"/> with
    /// <paramref name="proposedRole"/>, because the requirement framework does not return its policies for a brand-new
    /// or Staged member.
    /// </summary>
    /// <param name="organization">The organization the user is joining.</param>
    /// <param name="user">The user joining the organization.</param>
    /// <param name="proposedRole">
    /// The role the user will hold in the organization: their existing membership's role, or
    /// <see cref="OrganizationUserType.User"/> for a brand-new member.
    /// </param>
    /// <returns><c>null</c> when allowed, otherwise the error explaining why.</returns>
    Task<Error?> ValidateCanJoinAsync(Organization organization, User user, OrganizationUserType proposedRole);
}
