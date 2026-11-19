namespace Bit.Seeder.Data.Enums;

/// <summary>
/// How default collections draw their active item counts from the preset's size histogram.
/// </summary>
public enum DefaultCollectionSizing
{
    /// <summary>
    /// Random log-uniform draws inside each bucket, so the totals vary with the seed.
    /// </summary>
    Sampled,

    /// <summary>
    /// Each bucket's mid-quantiles, so the set of sizes and the totals are the same for every seed.
    /// </summary>
    Quantile,
}
