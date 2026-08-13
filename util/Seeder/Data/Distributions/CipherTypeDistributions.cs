using Bit.Core.Vault.Enums;

namespace Bit.Seeder.Data.Distributions;

/// <summary>
/// Pre-configured cipher type distributions for seeding scenarios.
/// </summary>
public static class CipherTypeDistributions
{
    /// <summary>
    /// Realistic enterprise mix based on typical usage patterns.
    /// 60% Login, 15% SecureNote, 12% Card, 10% Identity, 3% SshKey
    /// </summary>
    public static Distribution<CipherType> Realistic { get; } = new(
        (CipherType.Login, 0.60),
        (CipherType.SecureNote, 0.15),
        (CipherType.Card, 0.12),
        (CipherType.Identity, 0.10),
        (CipherType.SSHKey, 0.03)
    );

    /// <summary>
    /// Login-only distribution for backward compatibility or login-focused testing.
    /// </summary>
    public static Distribution<CipherType> LoginOnly { get; } = new(
        (CipherType.Login, 1.0)
    );

    /// <summary>
    /// Heavy on secure notes for documentation-focused organizations.
    /// </summary>
    public static Distribution<CipherType> DocumentationHeavy { get; } = new(
        (CipherType.Login, 0.40),
        (CipherType.SecureNote, 0.40),
        (CipherType.Card, 0.10),
        (CipherType.Identity, 0.07),
        (CipherType.SSHKey, 0.03)
    );

    /// <summary>
    /// Developer-focused with more SSH keys.
    /// </summary>
    public static Distribution<CipherType> DeveloperFocused { get; } = new(
        (CipherType.Login, 0.50),
        (CipherType.SecureNote, 0.20),
        (CipherType.Card, 0.05),
        (CipherType.Identity, 0.05),
        (CipherType.SSHKey, 0.20)
    );
}
