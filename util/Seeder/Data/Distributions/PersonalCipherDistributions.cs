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
}
