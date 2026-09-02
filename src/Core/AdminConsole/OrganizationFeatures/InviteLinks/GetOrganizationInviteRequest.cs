using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// The data required to retrieve the invite for an invite link. The invite is an opaque
/// cryptographic value that the server stores and transports but never inspects; it is decrypted
/// client-side to reconstruct and confirm the invite link.
/// </summary>
public record GetOrganizationInviteRequest
{
    /// <summary>
    /// The ID of the organization whose invite link the user is retrieving the invite for.
    /// </summary>
    public required Guid OrganizationId { get; init; }

    /// <summary>
    /// The secret code embedded in the invite link the user is retrieving the invite for.
    /// </summary>
    public required Guid Code { get; init; }

    /// <summary>
    /// The authenticated user requesting the invite.
    /// </summary>
    public required User User { get; init; }
}
