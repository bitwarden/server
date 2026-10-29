using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Business rules for invite links, shared by the invite link endpoints' validators. These rules do not read the
/// database or configuration; the caller loads the data they need.
/// </summary>
public static class InviteLinkRules
{
    /// <summary>
    /// Checks that the user's existing membership allows accepting an invite link: it must not be revoked, and the
    /// user must not already be an accepted or confirmed member.
    /// </summary>
    /// <param name="organization">The organization the user is joining.</param>
    /// <param name="existingOrganizationUser">The user's existing membership, or <c>null</c> if they have none.</param>
    /// <returns><c>null</c> when allowed, otherwise the error explaining why.</returns>
    public static Error? ValidateAcceptMembershipStatus(Organization organization, OrganizationUser? existingOrganizationUser) =>
        existingOrganizationUser?.Status switch
        {
            OrganizationUserStatusType.Revoked => new OrganizationAccessRevoked(organization.DisplayName()),
            OrganizationUserStatusType.Accepted or OrganizationUserStatusType.Confirmed =>
                new AlreadyOrganizationMember(organization.DisplayName()),
            _ => null
        };

    /// <summary>
    /// Checks that the user's existing membership allows confirming through an invite link: it must not be revoked,
    /// and the user must not already be confirmed. An accepted membership is allowed, so it can be confirmed.
    /// </summary>
    /// <param name="organization">The organization the user is joining.</param>
    /// <param name="existingOrganizationUser">The user's existing membership, or <c>null</c> if they have none.</param>
    /// <returns><c>null</c> when allowed, otherwise the error explaining why.</returns>
    public static Error? ValidateConfirmMembershipStatus(Organization organization, OrganizationUser? existingOrganizationUser) =>
        existingOrganizationUser switch
        {
            { RevocationReason: not null } => new OrganizationAccessRevoked(organization.DisplayName()),
            { Status: OrganizationUserStatusType.Confirmed } => new AlreadyOrganizationMember(organization.DisplayName()),
            _ => null
        };
}
