using Bit.Seeder.Data.Enums;

namespace Bit.Seeder.Data.Distributions;

/// <summary>
/// Pre-configured password strength distributions for seeding scenarios.
/// </summary>
public static class PasswordDistributions
{
    /// <summary>
    /// Realistic distribution based on breach data and security research.
    /// 25% VeryWeak, 30% Weak, 25% Fair, 15% Strong, 5% VeryStrong
    /// </summary>
    public static Distribution<PasswordStrength> Realistic { get; } = new(
        (PasswordStrength.VeryWeak, 0.25),
        (PasswordStrength.Weak, 0.30),
        (PasswordStrength.Fair, 0.25),
        (PasswordStrength.Strong, 0.15),
        (PasswordStrength.VeryStrong, 0.05)
    );
}
