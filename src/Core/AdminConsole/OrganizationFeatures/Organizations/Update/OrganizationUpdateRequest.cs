using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Update;

/// <summary>
/// Request model for updating the name, billing email, and/or public-private keys for an organization (legacy migration code).
/// Any combination of these properties can be updated, so they are optional. If none are specified it will not update anything.
/// </summary>
public record OrganizationUpdateRequest
{
    /// <summary>
    /// The ID of the organization to update.
    /// </summary>
    public required Guid OrganizationId { get; init; }

    /// <summary>
    /// The new organization name to apply (optional, this is skipped if not provided).
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The new billing email address to apply (optional, this is skipped if not provided).
    /// </summary>
    public string? BillingEmail { get; init; }

    /// <summary>
    /// The organization's public/private key pair to set (optional, only set if not already present on the organization).
    /// </summary>
    public PublicKeyEncryptionKeyPairData? Keys { get; init; }
}
