namespace Bit.Seeder.Data.Distributions;

/// <summary>
/// Pre-configured personal cipher count distributions per user.
/// </summary>
public static class PersonalCipherDistributions
{
    /// <summary>
    /// Realistic enterprise mix: 30% have none, power users have 50-200.
    /// </summary>
    public static Distribution<(int Min, int Max)> Realistic { get; } = new(
        ((0, 1), 0.30),
        ((1, 5), 0.25),
        ((5, 15), 0.25),
        ((15, 50), 0.15),
        ((50, 200), 0.05)
    );

    /// <summary>
    /// Light usage: most users don't use personal vaults.
    /// </summary>
    public static Distribution<(int Min, int Max)> LightUsage { get; } = new(
        ((0, 1), 0.60),
        ((1, 5), 0.30),
        ((5, 15), 0.10)
    );

    /// <summary>
    /// Heavy usage: power users dominate, everyone has personal items.
    /// </summary>
    public static Distribution<(int Min, int Max)> HeavyUsage { get; } = new(
        ((1, 5), 0.10),
        ((5, 20), 0.30),
        ((20, 100), 0.40),
        ((100, 500), 0.20)
    );

    /// <summary>
    /// A large org part-way through the Organization Data Ownership migration: personal items still waiting for
    /// members who have not logged in since the policy was enabled. ~80% have none and a few hold over a thousand,
    /// about 6 per confirmed member overall (prod: 22.6k items across 718 of 3,644 confirmed members, 3 above 1,000).
    /// </summary>
    public static Distribution<(int Min, int Max)> PendingMigration { get; } = new(
        ((0, 0), 0.8026),
        ((1, 10), 0.129),
        ((11, 40), 0.042),
        ((51, 150), 0.022),
        ((201, 480), 0.0036),
        ((1001, 2000), 0.0008)
    );
}
