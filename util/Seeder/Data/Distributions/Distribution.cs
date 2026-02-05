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
    /// Items 0 to (total * percentage1 - 1) get value1, and so on.
    /// </summary>
    /// <param name="index">Zero-based index of the item.</param>
    /// <param name="total">Total number of items being distributed. For best accuracy, use totals >= 100.</param>
    /// <returns>The value assigned to this index position.</returns>
    public T Select(int index, int total)
    {
        var cumulative = 0;
        foreach (var (value, percentage) in _buckets)
        {
            cumulative += (int)(total * percentage);
            if (index < cumulative)
            {
                return value;
            }
        }
        return _buckets[^1].Value;
    }

    /// <summary>
    /// Returns all values with their calculated counts for a given total.
    /// The last bucket receives any remainder from rounding.
    /// </summary>
    /// <param name="total">Total number of items to distribute.</param>
    /// <returns>Sequence of value-count pairs.</returns>
    public IEnumerable<(T Value, int Count)> GetCounts(int total)
    {
        var remaining = total;
        for (var i = 0; i < _buckets.Length - 1; i++)
        {
            var count = (int)(total * _buckets[i].Percentage);
            yield return (_buckets[i].Value, count);
            remaining -= count;
        }
        yield return (_buckets[^1].Value, remaining);
    }
}
