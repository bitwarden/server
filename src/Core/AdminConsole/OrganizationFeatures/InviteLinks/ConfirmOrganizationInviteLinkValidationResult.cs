using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// The validated context produced by <see cref="IConfirmOrganizationInviteLinkValidator"/> when a user
/// is eligible to be confirmed via an invite link. The confirmation endpoints reuse this so they do not
/// have to re-resolve the link, organization, or existing membership after validation succeeds.
/// </summary>
public record ConfirmOrganizationInviteLinkValidationResult
{
    /// <summary>
    /// The invite link resolved from the request code.
    /// </summary>
    public required OrganizationInviteLink InviteLink { get; init; }

    /// <summary>
    /// The organization the invite link belongs to.
    /// </summary>
    public required Organization Organization { get; init; }

    /// <summary>
    /// The user's existing membership in the organization, or <see langword="null"/> when the user is
    /// not yet a member and a new membership will be created during confirmation.
    /// </summary>
    public OrganizationUser? ExistingOrganizationUser { get; init; }
}
