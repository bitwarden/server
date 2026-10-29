using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

/// <summary>
/// Read-only checks shared by the invite link validators that need to load data.
/// </summary>
public interface IInviteLinkValidationService
{
    /// <summary>
    /// Checks that a user can join the organization, in this order:
    /// <list type="bullet">
    ///     <item>The free organization admin limit.</item>
    ///     <item>The Automatic User Confirmation policy.</item>
    ///     <item>The Single Organization policy.</item>
    ///     <item>The Require Two-Factor Authentication policy.</item>
    /// </list>
    /// </summary>
    /// <param name="organization">The organization being joined.</param>
    /// <param name="user">The user joining.</param>
    /// <param name="proposedRole">The user's existing role, or <see cref="OrganizationUserType.User"/> if new.</param>
    /// <param name="policyEnforcer">The policy enforcer for the organization being joined.</param>
    Task<JoinRequirementsValidationResult> ValidateJoinRequirementsAsync(
        Organization organization,
        User user,
        OrganizationUserType proposedRole,
        IPreAccessPolicyEnforcer policyEnforcer);
}
