using Bit.Core.Billing.Enums;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Data.Distributions;
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
    /// When false or Users less than 10, all users are Confirmed.
    /// </summary>
    public bool RealisticStatusMix { get; init; } = false;

    /// <summary>
    /// Org structure for realistic collection names.
    /// </summary>
    public OrgStructureModel? StructureModel { get; init; }

    /// <summary>
    /// Username pattern for corporate email format (e.g., first.last@domain).
    /// Only applies to CorporateEmail category usernames.
    /// </summary>
    public UsernamePatternType UsernamePattern { get; init; } = UsernamePatternType.FirstDotLast;

    /// <summary>
    /// Distribution of username categories (corporate email, personal email, social handles, etc.).
    /// Use <see cref="UsernameDistributions.Realistic"/> for a typical enterprise mix (45% corporate).
    /// </summary>
    public Distribution<UsernameCategory> UsernameDistribution { get; init; } = UsernameDistributions.Realistic;

    /// <summary>
    /// Distribution of password strengths for cipher logins.
    /// Use <see cref="PasswordDistributions.Realistic"/> for breach-data distribution
    /// (25% VeryWeak, 30% Weak, 25% Fair, 15% Strong, 5% VeryStrong).
    /// </summary>
    public Distribution<PasswordStrength> PasswordDistribution { get; init; } = PasswordDistributions.Realistic;

    /// <summary>
    /// Geographic region for culturally-appropriate name generation in cipher usernames.
    /// Defaults to Global (mixed locales from all regions).
    /// </summary>
    public GeographicRegion? Region { get; init; }

    /// <summary>
    /// When specified, ciphers are distributed according to the percentages.
    /// Use <see cref="CipherTypeDistributions.Realistic"/> for a typical enterprise mix.
    /// </summary>
    public Distribution<CipherType> CipherTypeDistribution { get; init; } = CipherTypeDistributions.Realistic;

    /// <summary>
    /// Density profile controlling entity relationship patterns.
    /// When null, steps use default round-robin behavior.
    /// </summary>
    public DensityProfile? Density { get; init; }

    /// <summary>
    /// Seed for deterministic data generation. When null, derived from Domain hash.
    /// </summary>
    public int? Seed { get; init; }

    /// <summary>
    /// Password for all seeded accounts. Defaults to "asdfasdfasdf" if not specified.
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// Billing plan type for the organization.
    /// </summary>
    public PlanType PlanType { get; init; } = PlanType.EnterpriseAnnually;
}
