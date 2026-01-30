using Bit.Core.Entities;

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
    /// The organization's public key.
    /// </summary>
    public required string PublicKey { get; init; }

    /// <summary>
    /// The organization's encrypted private key.
    /// </summary>
    public required string PrivateKey { get; init; }

    /// <summary>
    /// The name of the default collection to create. Optional - if null or empty, no collection is created.
    /// </summary>
    public string? CollectionName { get; init; }

    /// <summary>
    /// The email token for validating the invitation.
    /// </summary>
    public required string EmailToken { get; init; }

    /// <summary>
    /// The user's key encrypted with the organization's key.
    /// </summary>
    public required string UserKey { get; init; }
}
