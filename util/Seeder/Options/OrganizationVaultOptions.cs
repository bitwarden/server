using Bit.Seeder.Data.Enums;

namespace Bit.Seeder.Options;

/// <summary>
/// Options for seeding an organization with vault data.
/// </summary>
public class OrganizationVaultOptions
{
    /// <summary>
    /// Organization name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Domain for user emails (e.g., "example.com").
    /// </summary>
    public required string Domain { get; init; }

    /// <summary>
    /// Number of member users to create.
    /// </summary>
    public required int Users { get; init; }

    /// <summary>
    /// Number of login ciphers to create.
    /// </summary>
    public int Ciphers { get; init; } = 0;

    /// <summary>
    /// Number of groups to create.
    /// </summary>
    public int Groups { get; init; } = 0;

    /// <summary>
    /// When true and Users >= 10, creates a realistic mix of user statuses:
    /// 85% Confirmed, 5% Invited, 5% Accepted, 5% Revoked.
    /// When false or Users &lt; 10, all users are Confirmed.
    /// </summary>
    public bool RealisticStatusMix { get; init; } = false;

    /// <summary>
    /// Org structure for realistic collection names.
    /// </summary>
    public OrgStructureModel? StructureModel { get; init; }

    /// <summary>
    /// Username pattern for cipher logins.
    /// </summary>
    public UsernamePatternType UsernamePattern { get; init; } = UsernamePatternType.FirstDotLast;

    /// <summary>
    /// Password strength for cipher logins. Defaults to Realistic distribution
    /// (25% VeryWeak, 30% Weak, 25% Fair, 15% Strong, 5% VeryStrong).
    /// </summary>
    public PasswordStrength PasswordStrength { get; init; } = PasswordStrength.Realistic;
}
