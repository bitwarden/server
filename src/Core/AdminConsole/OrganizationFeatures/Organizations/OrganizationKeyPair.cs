namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

/// <summary>
/// Data transfer object for organization public/private key pairs.
/// Used to normalize key handling across different request models and commands.
/// </summary>
public record OrganizationKeyPair
{
    public string? PublicKey { get; init; }
    public string? PrivateKey { get; init; }
}
