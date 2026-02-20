using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

/// <summary>
/// Request model for initializing a pending organization with upfront validation.
/// </summary>
public record InitPendingOrganizationRequest
{
    /// <summary>
    /// The user who is accepting the organization invitation.
    /// </summary>
    public required User User { get; init; }

    /// <summary>
    /// The ID of the organization to initialize.
    /// </summary>
    public required Guid OrganizationId { get; init; }

    /// <summary>
    /// The ID of the organization user record.
    /// </summary>
    public required Guid OrganizationUserId { get; init; }

    /// <summary>
    /// The organization's encryption key pair (public key and wrapped private key).
    /// </summary>
    public required PublicKeyEncryptionKeyPairData OrganizationKeys { get; init; }

    /// <summary>
    /// The name of the default collection to create. Optional - if null or empty, no collection is created.
    /// </summary>
    public string? CollectionName { get; init; }

    /// <summary>
    /// The email token for validating the invitation.
    /// </summary>
    public required string EmailToken { get; init; }

    /// <summary>
    /// The Organization symmetric key encrypted with the User's public key.
    /// </summary>
    public required string EncryptedOrganizationSymmetricKey { get; init; }
}
