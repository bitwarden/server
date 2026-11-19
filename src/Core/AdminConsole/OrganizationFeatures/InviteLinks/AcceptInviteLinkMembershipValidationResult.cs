using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// The validated context needed to accept an invite link once the user is found eligible.
/// </summary>
public record AcceptInviteLinkMembershipValidationResult
{
    /// <summary>
    /// The organization the invite link belongs to.
    /// </summary>
    public required Organization Organization { get; init; }

    /// <summary>
    /// The user's existing membership in the organization, if any. Non-null only for a pending email
    /// invitation (an <c>Invited</c> row) or a Staged provisioning row. Null for a brand-new member joining
    /// purely via the link.
    /// </summary>
    public OrganizationUser? ExistingOrganizationUser { get; init; }

    /// <summary>
    /// If true, the caller must remove the user's emergency access and send the auto-confirm notification.
    /// </summary>
    public bool AutoConfirmPolicyEnabled { get; init; }

    /// <summary>
    /// If true, the caller must enroll the user in account recovery once they join.
    /// </summary>
    public bool AccountRecoveryAutoEnrollEnabled { get; init; }
}
