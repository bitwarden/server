using Bit.Seeder.Data.Enums;

namespace Bit.Seeder.Data.Distributions;

/// <summary>
/// Pre-configured username category distributions for seeding scenarios.
/// Pass to CipherUsernameGenerator for different username mixes.
/// </summary>
public static class UsernameDistributions
{
    /// <summary>
    /// Realistic enterprise mix with variety.
    /// 45% Corporate email, varied personal/legacy/social.
    /// </summary>
    public static Distribution<UsernameCategory> Realistic { get; } = new(
        (UsernameCategory.CorporateEmail, 0.45),
        (UsernameCategory.PersonalEmail, 0.15),
        (UsernameCategory.SocialHandle, 0.10),
        (UsernameCategory.UsernameOnly, 0.10),
        (UsernameCategory.EmployeeId, 0.08),
        (UsernameCategory.PhoneNumber, 0.05),
        (UsernameCategory.LegacySystem, 0.04),
        (UsernameCategory.RandomAlphanumeric, 0.03)
    );

    /// <summary>
    /// Corporate-only: 100% corporate email format.
    /// Use for strict enterprise environments.
    /// </summary>
    public static Distribution<UsernameCategory> CorporateOnly { get; } = new(
        (UsernameCategory.CorporateEmail, 1.0)
    );

    /// <summary>
    /// Consumer-focused: personal emails and social handles.
    /// Use for B2C application testing.
    /// </summary>
    public static Distribution<UsernameCategory> Consumer { get; } = new(
        (UsernameCategory.PersonalEmail, 0.40),
        (UsernameCategory.SocialHandle, 0.25),
        (UsernameCategory.UsernameOnly, 0.20),
        (UsernameCategory.PhoneNumber, 0.15)
    );

    /// <summary>
    /// Legacy enterprise: older systems with employee IDs.
    /// Use for testing migrations from legacy systems.
    /// </summary>
    public static Distribution<UsernameCategory> LegacyEnterprise { get; } = new(
        (UsernameCategory.CorporateEmail, 0.30),
        (UsernameCategory.EmployeeId, 0.30),
        (UsernameCategory.LegacySystem, 0.25),
        (UsernameCategory.RandomAlphanumeric, 0.15)
    );

    /// <summary>
    /// Developer-focused: mix of corporate and technical identifiers.
    /// </summary>
    public static Distribution<UsernameCategory> Developer { get; } = new(
        (UsernameCategory.CorporateEmail, 0.35),
        (UsernameCategory.UsernameOnly, 0.25),
        (UsernameCategory.SocialHandle, 0.20),
        (UsernameCategory.RandomAlphanumeric, 0.20)
    );
}
