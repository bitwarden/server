using Bit.Core.Billing.Enums;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;

namespace Bit.Seeder.Options;

public class OrganizationVaultOptions
{
    public required string Name { get; init; }

    public required string Domain { get; init; }

    public required int Users { get; init; }

    public int Ciphers { get; init; } = 0;

    public int Groups { get; init; } = 0;

    /// <summary>
    /// When true and Users >= 10, creates a realistic mix of user statuses:
    /// 85% Confirmed, 5% Invited, 5% Accepted, 5% Revoked.
    /// When false or Users &lt; 10, all users are Confirmed.
    /// </summary>
    public bool RealisticStatusMix { get; init; } = false;

    public OrgStructureModel? StructureModel { get; init; }

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

    public GeographicRegion? Region { get; init; }

    public Distribution<CipherType> CipherTypeDistribution { get; init; } = CipherTypeDistributions.Realistic;

    /// <summary>
    /// When null, derived from Domain hash for deterministic data generation.
    /// </summary>
    public int? Seed { get; init; }

    /// <summary>
    /// Password for all seeded accounts. Defaults to "asdfasdfasdf" if not specified.
    /// </summary>
    public string? Password { get; init; }

    public PlanType PlanType { get; init; } = PlanType.EnterpriseAnnually;
}
