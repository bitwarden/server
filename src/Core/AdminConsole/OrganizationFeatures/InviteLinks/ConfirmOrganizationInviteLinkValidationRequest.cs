using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// The data required to determine whether a user is eligible to be confirmed into an organization
/// via an invite link. This is the input for the read-only precheck performed before the
/// org key is released and before the user is confirmed.
/// </summary>
public record ConfirmOrganizationInviteLinkValidationRequest
{
    /// <summary>
    /// The organization's invite link, or <see langword="null"/> if it has none.
    /// </summary>
    public required OrganizationInviteLink? InviteLink { get; init; }

    /// <summary>
    /// The secret code embedded in the invite link the user is attempting to use.
    /// </summary>
    public required Guid Code { get; init; }

    /// <summary>
    /// The organization the invite link belongs to, or <see langword="null"/> if it does not exist.
    /// </summary>
    public required Organization? Organization { get; init; }

    /// <summary>
    /// The authenticated user attempting to confirm their membership.
    /// </summary>
    public required User User { get; init; }

    /// <summary>
    /// The user's existing membership in the organization, or <see langword="null"/> when the user is
    /// not yet a member and a new membership will be created during confirmation.
    /// </summary>
    public OrganizationUser? ExistingOrganizationUser { get; init; }
}
