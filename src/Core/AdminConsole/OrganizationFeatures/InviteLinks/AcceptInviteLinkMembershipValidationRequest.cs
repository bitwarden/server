using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Request for <see cref="IAcceptInviteLinkMembershipValidator"/>. Carries everything needed to decide
/// whether a user may accept an invite link into <see cref="Organization"/>, including the organization
/// record (so target-org policies can be read directly for brand-new members) and the user's existing
/// memberships.
/// </summary>
public record AcceptInviteLinkMembershipValidationRequest
{
    public required Organization Organization { get; init; }
    public required User User { get; init; }
    public required IEnumerable<string> AllowedDomains { get; init; }
    public required ICollection<OrganizationUser> AllOrganizationMemberships { get; init; }

    /// <summary>
    /// The user's existing membership in the organization, if any. Non-null only for a pending email
    /// invitation (an <c>Invited</c> row). Null for a brand-new member joining purely via the link.
    /// </summary>
    public OrganizationUser? ExistingMembership { get; init; }

    public string? ResetPasswordKey { get; init; }
}
