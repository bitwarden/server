namespace Bit.Seeder.Data.Distributions;

/// <summary>
/// Draws long-tailed integer sizes from an empirical histogram of [Min, Max] buckets.
/// Bucket counts are exact (largest-remainder, via <see cref="Distribution{T}"/>); values within a
/// bucket are log-uniform so wide buckets keep their tail shape. Deterministic for a given <see cref="Random"/>.
/// </summary>
internal sealed class SizeHistogram
{
    private readonly Distribution<(int Min, int Max)> _distribution;

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

    /// <summary>
    /// Scales the non-zero entries of <paramref name="sizes"/> so they sum to <paramref name="target"/>,
    /// keeping every non-zero entry at least 1 and at most <paramref name="cap"/>.
    /// </summary>
    internal static void ScaleTo(int[] sizes, int target, int cap)
    {
        var current = sizes.Sum(s => (long)s);
        if (current == 0 || target <= 0)
        {
            return;
        }

        var factor = (double)target / current;
        for (var i = 0; i < sizes.Length; i++)
        {
            if (sizes[i] > 0)
            {
                sizes[i] = Math.Clamp((int)Math.Round(sizes[i] * factor), 1, cap);
            }
        }

        // Rounding drift: nudge the largest entries until the total matches
        var diff = target - sizes.Sum();
        var order = Enumerable.Range(0, sizes.Length).Where(i => sizes[i] > 0)
            .OrderByDescending(i => sizes[i]).ToArray();
        for (var k = 0; diff != 0 && order.Length > 0; k++)
        {
            var i = order[k % order.Length];
            var step = Math.Sign(diff);
            if (sizes[i] + step >= 1 && sizes[i] + step <= cap)
            {
                sizes[i] += step;
                diff -= step;
            }
            else if (k > order.Length * 4)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Pareto-distributed weights (x_min = 1), used to give members a heavy-tailed affinity.
    /// </summary>
    internal static double[] ParetoWeights(int count, double alpha, Random random)
    {
        var weights = new double[count];
        for (var i = 0; i < count; i++)
        {
            weights[i] = Math.Pow(1.0 - random.NextDouble(), -1.0 / alpha);
        }
        return weights;
    }

    /// <summary>
    /// Weighted sampling without replacement (Efraimidis–Spirakis). Indices whose weight is 0 or that
    /// <paramref name="eligible"/> rejects are skipped.
    /// </summary>
    internal static List<int> SampleWithoutReplacement(
        double[] weights, int take, Random random, Func<int, bool>? eligible = null)
    {
        var keyed = new List<(double Key, int Index)>(weights.Length);
        for (var i = 0; i < weights.Length; i++)
        {
            if (weights[i] <= 0 || (eligible is not null && !eligible(i)))
            {
                continue;
            }
            keyed.Add((Math.Pow(random.NextDouble(), 1.0 / weights[i]), i));
        }

        return keyed.OrderByDescending(k => k.Key).Take(take).Select(k => k.Index).ToList();
    }
}
