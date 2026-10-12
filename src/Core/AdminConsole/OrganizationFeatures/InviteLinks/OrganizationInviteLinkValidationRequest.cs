using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// The data the invite link validators need to decide whether a user may use an invite link. The caller looks it up
/// and passes it in. Used by <see cref="Interfaces.IGetOrganizationInviteValidator"/>.
/// </summary>
public record OrganizationInviteLinkValidationRequest
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
    /// The authenticated user using the invite link.
    /// </summary>
    public required User User { get; init; }

    /// <summary>
    /// The user's existing membership in the organization, or <see langword="null"/> when the user is
    /// not yet a member.
    /// </summary>
    public OrganizationUser? ExistingOrganizationUser { get; init; }
}
