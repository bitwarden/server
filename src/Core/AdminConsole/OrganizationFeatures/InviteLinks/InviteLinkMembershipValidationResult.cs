using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// The validated context produced by <see cref="IAcceptInviteLinkMembershipValidator"/> and
/// <see cref="IConfirmOrganizationInviteLinkValidator"/> when a user is eligible to join via an invite link.
/// </summary>
public record InviteLinkMembershipValidationResult
{
    /// <summary>
    /// The organization the invite link belongs to.
    /// </summary>
    public required Organization Organization { get; init; }

    /// <summary>
    /// The user's existing membership in the organization, or <see langword="null"/> when a new membership will be
    /// created.
    /// </summary>
    public OrganizationUser? ExistingOrganizationUser { get; init; }

    /// <summary>
    /// If true, the user must be enrolled in account recovery once they join.
    /// </summary>
    public bool AccountRecoveryAutoEnrollEnabled { get; init; }

    /// <summary>
    /// If true, the Automatic User Confirmation policy is enforced for the user.
    /// </summary>
    public bool AutoConfirmPolicyEnabled { get; init; }
}
