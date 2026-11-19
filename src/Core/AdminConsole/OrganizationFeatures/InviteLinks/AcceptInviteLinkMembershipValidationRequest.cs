using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Request for <see cref="IAcceptInviteLinkMembershipValidator"/>. Carries everything needed to decide
/// whether a user may accept an organization's invite link.
/// </summary>
public record AcceptInviteLinkMembershipValidationRequest
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
    /// The authenticated user attempting to accept the invite link.
    /// </summary>
    public required User User { get; init; }

    /// <summary>
    /// The user's account recovery (reset password) key, if supplied.
    /// </summary>
    public string? ResetPasswordKey { get; init; }
}
