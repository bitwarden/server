namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

/// <summary>
/// Data transfer object for organization public/private key pairs.
/// Used to normalize key handling across different request models and commands.
/// </summary>
public record OrganizationKeyPair
{
    public required string PublicKey { get; init; }
    public required string PrivateKey { get; init; }
}
