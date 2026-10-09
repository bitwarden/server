using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Request for <see cref="Interfaces.IInviteLinkEligibilityValidator"/>.
/// </summary>
public record InviteLinkEligibilityValidationRequest
{
    /// <summary>
    /// The organization's invite link, or <see langword="null"/> if it has none.
    /// </summary>
    public required OrganizationInviteLink? InviteLink { get; init; }

    /// <summary>
    /// The secret code from the invite link the user is using.
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
}
