using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Request for <see cref="IInviteLinkEligibilityValidator"/>.
/// </summary>
public record InviteLinkEligibilityValidationRequest
{
    /// <summary>
    /// The organization whose invite link the user is using.
    /// </summary>
    public required Guid OrganizationId { get; init; }

    /// <summary>
    /// The secret code embedded in the invite link.
    /// </summary>
    public required Guid Code { get; init; }

    /// <summary>
    /// The authenticated user using the invite link.
    /// </summary>
    public required User User { get; init; }
}
