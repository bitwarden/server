using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Business rules for invite links, shared by the invite link endpoints' validators. These rules do not read the
/// database; the caller loads the data they need.
/// </summary>
public static class InviteLinkRules
{
    /// <summary>
    /// Checks that the invite link can be used by the user, in this order:
    /// <list type="bullet">
    /// <item>The invite link exists and its code matches.</item>
    /// <item>The organization exists, is enabled, and supports invite links.</item>
    /// <item>The user's email domain is on the link's allowed domain list.</item>
    /// <item>The user's email is verified.</item>
    /// </list>
    /// </summary>
    /// <param name="inviteLink">The organization's invite link, or <c>null</c> if it has none.</param>
    /// <param name="code">The secret code from the invite link the user is using.</param>
    /// <param name="organization">The organization the invite link belongs to, or <c>null</c> if it does not exist.</param>
    /// <param name="user">The authenticated user using the invite link.</param>
    /// <returns><c>null</c> when allowed, otherwise the error explaining why.</returns>
    public static Error? ValidateEligibility(OrganizationInviteLink? inviteLink, Guid code, Organization? organization, User user)
    {
        if (inviteLink is null || !inviteLink.CodeMatches(code.ToString()))
        {
            return new InviteLinkNotFound();
        }

        if (organization is null or { Enabled: false })
        {
            return new InviteLinkNotFound();
        }

        if (!organization.UseInviteLinks)
        {
            return new InviteLinkNotAvailable();
        }

        // The domain is checked before email verification so a user on a disallowed domain is told so first,
        // rather than being sent through the client's email verification step only to be rejected afterwards.
        // Auth's invite link email verification flow (PM-42435) relies on this order; coordinate with Auth before
        // changing it.
        if (!InviteLinkDomainValidator.IsEmailDomainAllowed(user.Email, inviteLink.GetAllowedDomains()))
        {
            return new EmailDomainNotAllowed(organization.DisplayName());
        }

        // Security: allowed domains only provide protection if the user has proven control over their email address.
        if (!user.EmailVerified)
        {
            return new EmailNotVerified();
        }

        return null;
    }

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
