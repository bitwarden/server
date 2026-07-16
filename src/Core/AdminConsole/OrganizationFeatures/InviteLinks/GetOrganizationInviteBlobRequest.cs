using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// The data required to retrieve the invite blob for an invite link. The blob is an opaque
/// cryptographic value that the server stores and transports but never inspects; it is decrypted
/// client-side to reconstruct and confirm the invite link.
/// </summary>
public record GetOrganizationInviteBlobRequest
{
    /// <summary>
    /// The ID of the organization whose invite link the user is retrieving the blob for.
    /// </summary>
    public required Guid OrganizationId { get; init; }

    /// <summary>
    /// The secret code embedded in the invite link the user is retrieving the blob for.
    /// </summary>
    public required Guid Code { get; init; }

    /// <summary>
    /// The authenticated user requesting the invite blob.
    /// </summary>
    public required User User { get; init; }
}
