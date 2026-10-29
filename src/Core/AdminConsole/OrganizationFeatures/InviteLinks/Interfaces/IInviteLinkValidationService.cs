using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

/// <summary>
/// Read-only checks shared by the invite link validators.
/// </summary>
public interface IInviteLinkValidationService
{
    /// <summary>
    /// Checks that the invite link can be used by the user, in this order:
    /// <list type="bullet">
    /// <item>The invite link exists and its code matches.</item>
    /// <item>The organization exists, is enabled, and supports invite links.</item>
    /// <item>The user's email domain is on the link's allowed domain list.</item>
    /// <item>The user's email is verified, unless a self-hosted instance has disabled this check.</item>
    /// </list>
    /// </summary>
    /// <param name="inviteLink">The organization's invite link, or <c>null</c> if it has none.</param>
    /// <param name="code">The secret code from the invite link the user is using.</param>
    /// <param name="organization">The organization the invite link belongs to, or <c>null</c> if it does not exist.</param>
    /// <param name="user">The authenticated user using the invite link.</param>
    /// <returns><c>null</c> when allowed, otherwise the error explaining why.</returns>
    Error? ValidateEligibility(
        OrganizationInviteLink? inviteLink,
        Guid code,
        Organization? organization,
        User user);

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
