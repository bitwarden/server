namespace Bit.Seeder.Data.Distributions;

/// <summary>
/// Provides deterministic, percentage-based item selection for test data generation.
/// Replaces duplicated distribution logic in GetRealisticStatus, GetFolderCountForUser, etc.
/// </summary>
/// <typeparam name="T">The type of values in the distribution.</typeparam>
public sealed class Distribution<T>
{
    private readonly (T Value, double Percentage)[] _buckets;

    /// <summary>
    /// Creates a distribution from percentage buckets.
    /// </summary>
    /// <param name="buckets">Value-percentage pairs that must sum to 1.0 (within 0.001 tolerance).</param>
    /// <exception cref="ArgumentException">Thrown when percentages don't sum to 1.0.</exception>
    public Distribution(params (T Value, double Percentage)[] buckets)
    {
        var total = buckets.Sum(b => b.Percentage);
        if (Math.Abs(total - 1.0) > 0.001)
        {
            throw new ArgumentException($"Percentages must sum to 1.0, got {total}");
        }
        _buckets = buckets;
    }

    /// <summary>
    /// Selects a value deterministically based on index position within a total count.
    /// Remainder items go to buckets with the largest fractional parts,
    /// not unconditionally to the last bucket.
    /// </summary>
    /// <param name="index">Zero-based index of the item.</param>
    /// <param name="total">Total number of items being distributed.</param>
    /// <returns>The value assigned to this index position.</returns>
    public T Select(int index, int total)
    {
        var cumulative = 0;
        foreach (var (value, count) in GetCounts(total))
        {
            cumulative += count;
            if (index < cumulative)
            {
                return value;
            }
        }

        return _buckets[^1].Value;
    }

    /// <summary>
    /// Returns all values with their calculated counts for a given total.
    /// Each bucket gets its truncated share, then the deficit is distributed one-at-a-time
    /// to buckets with the largest fractional remainders.
    /// Zero-weight buckets always receive exactly zero items.
    /// </summary>
    /// <param name="total">Total number of items to distribute.</param>
    /// <returns>Sequence of value-count pairs.</returns>
    public IEnumerable<(T Value, int Count)> GetCounts(int total)
    {
        var counts = new int[_buckets.Length];
        var remainders = new double[_buckets.Length];
        var allocated = 0;

        for (var i = 0; i < _buckets.Length; i++)
        {
            var exact = total * _buckets[i].Percentage;
            counts[i] = (int)exact;
            remainders[i] = exact - counts[i];
            allocated += counts[i];
        }

        var deficit = total - allocated;
        for (var d = 0; d < deficit; d++)
        {
            var bestIdx = 0;
            for (var i = 1; i < remainders.Length; i++)
            {
                if (remainders[i] > remainders[bestIdx])
                {
                    bestIdx = i;
                }
            }

            counts[bestIdx]++;
            remainders[bestIdx] = -1.0;
        }

        for (var i = 0; i < _buckets.Length; i++)
        {
            yield return (_buckets[i].Value, counts[i]);
        }
    }
}
