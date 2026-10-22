using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Request for <see cref="IConfirmOrganizationInviteLinkValidator"/>.
/// </summary>
public record ConfirmOrganizationInviteLinkValidationRequest
{
    /// <summary>
    /// The organization the user is attempting to join.
    /// </summary>
    public required Guid OrganizationId { get; init; }

    /// <summary>
    /// The secret code embedded in the invite link the user is attempting to use.
    /// </summary>
    public required Guid Code { get; init; }

    /// <summary>
    /// The authenticated user attempting to confirm their membership.
    /// </summary>
    public required User User { get; init; }

    /// <summary>
    /// The user's account recovery (reset password) key, if supplied.
    /// </summary>
    public string? ResetPasswordKey { get; init; }
}
