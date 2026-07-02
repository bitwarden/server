using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// The data required to determine whether a user is eligible to be confirmed into an organization
/// via an invite link. This is the shared input for the read-only precheck performed before the
/// org key is released and before the user is confirmed.
/// </summary>
public record ConfirmOrganizationInviteLinkValidationRequest
{
    /// <summary>
    /// The secret code embedded in the invite link the user is attempting to use.
    /// </summary>
    public required Guid Code { get; init; }

    /// <summary>
    /// The authenticated user attempting to confirm their membership.
    /// </summary>
    public required User User { get; init; }
}
