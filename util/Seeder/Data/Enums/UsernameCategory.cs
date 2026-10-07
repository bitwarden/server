namespace Bit.Seeder.Data.Enums;

/// <summary>
/// Categories of username formats found in real-world credential vaults.
/// Used with Distribution&lt;UsernameCategory&gt; for realistic username generation.
/// </summary>
public enum UsernameCategory
{
    /// <summary>
    /// Corporate email format: john.smith@acme.com
    /// </summary>
    CorporateEmail,

    /// <summary>
    /// Personal email: jsmith99@fake-gmail.com
    /// </summary>
    PersonalEmail,

    /// <summary>
    /// Social media handle: @john_smith_42
    /// </summary>
    SocialHandle,

    /// <summary>
    /// Plain username: johnsmith, jdoe1985
    /// </summary>
    UsernameOnly,

    /// <summary>
    /// Employee identifier: EMP001234, E-12345
    /// </summary>
    EmployeeId,

    /// <summary>
    /// Phone number as username: 15551234567
    /// </summary>
    PhoneNumber,

    /// <summary>
    /// Legacy system format: JSMITH01, DOEJ
    /// </summary>
    LegacySystem,

    /// <summary>
    /// Random alphanumeric: xK7mP9qR2n
    /// </summary>
    RandomAlphanumeric
}
