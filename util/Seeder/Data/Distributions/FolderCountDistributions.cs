namespace Bit.Seeder.Data.Distributions;

/// <summary>
/// Pre-configured folder count distributions for user vault seeding.
/// </summary>
public static class FolderCountDistributions
{
    /// <summary>
    /// Realistic distribution of folders per user.
    /// 35% have zero, 35% have 1-3, 20% have 4-7, 10% have 10-15.
    /// Values are (Min, Max) ranges for deterministic selection.
    /// </summary>
    public static Distribution<(int Min, int Max)> Realistic { get; } = new(
        ((0, 1), 0.35),
        ((1, 4), 0.35),
        ((4, 8), 0.20),
        ((10, 16), 0.10)
    );
}
