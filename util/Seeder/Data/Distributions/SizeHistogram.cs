namespace Bit.Seeder.Data.Distributions;

/// <summary>
/// Draws long-tailed integer sizes from an empirical histogram of [Min, Max] buckets.
/// Bucket counts are exact (largest-remainder, via <see cref="Distribution{T}"/>); values within a
/// bucket are log-uniform so wide buckets keep their tail shape. Deterministic for a given <see cref="Random"/>.
/// </summary>
internal sealed class SizeHistogram
{
    private readonly Distribution<(int Min, int Max)> _distribution;

    /// <summary>
    /// Normalizes the bucket weights into a distribution.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when there are no buckets, or a bucket has a negative bound,
    /// <c>Max</c> below <c>Min</c>, or a negative weight, or the weights total zero.</exception>
    internal SizeHistogram(IEnumerable<(int Min, int Max, double Weight)> buckets)
    {
        var list = buckets.ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("A size histogram needs at least one bucket.");
        }

        if (list.Any(b => b.Min < 0 || b.Max < b.Min || b.Weight < 0))
        {
            throw new ArgumentException("Histogram buckets need 0 <= min <= max and a non-negative weight.");
        }

        var total = list.Sum(b => b.Weight);
        if (total == 0)
        {
            throw new ArgumentException("Histogram buckets need a positive total weight.");
        }

        _distribution = new Distribution<(int Min, int Max)>(
            list.Select(b => ((b.Min, b.Max), b.Weight / total)).ToArray());
    }

    /// <summary>
    /// Returns <paramref name="count"/> sizes in random order.
    /// </summary>
    internal int[] Draw(int count, Random random)
    {
        var sizes = new List<int>(count);
        foreach (var ((min, max), bucketCount) in _distribution.GetCounts(count))
        {
            for (var i = 0; i < bucketCount; i++)
            {
                sizes.Add(LogUniform(min, max, random));
            }
        }

        var result = sizes.ToArray();
        random.Shuffle(result);
        return result;
    }

    private static int LogUniform(int min, int max, Random random)
    {
        if (min == max)
        {
            return min;
        }

        var lo = Math.Log(min + 1);
        var hi = Math.Log(max + 2);
        var value = (int)Math.Exp(lo + random.NextDouble() * (hi - lo)) - 1;
        return Math.Clamp(value, min, max);
    }
}
