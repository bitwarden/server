namespace Bit.Seeder.Data.Distributions;

/// <summary>
/// Pre-configured folder count distributions for user vault seeding.
/// </summary>
public static class FolderCountDistributions
{
    /// <summary>
    /// Realistic distribution of folders per user.
    /// 35% have 0-1, 35% have 1-4, 20% have 4-8, 10% have 10-16.
    /// Values are (Min, Max) ranges for deterministic selection.
    /// </summary>
    public static Distribution<(int Min, int Max)> Realistic { get; } = new(
        ((0, 1), 0.35),
        ((1, 4), 0.35),
        ((4, 8), 0.20),
        ((10, 16), 0.10)
    );

    /// <summary>
    /// Enterprise: more structured organizations with heavier folder usage.
    /// </summary>
    public static Distribution<(int Min, int Max)> Enterprise { get; } = new(
        ((0, 1), 0.20),
        ((2, 5), 0.30),
        ((5, 10), 0.30),
        ((10, 25), 0.20)
    );

    /// <summary>
    /// Minimal: most users don't bother organizing into folders.
    /// </summary>
    public static Distribution<(int Min, int Max)> Minimal { get; } = new(
        ((0, 1), 0.70),
        ((1, 3), 0.25),
        ((3, 6), 0.05)
    );
}
