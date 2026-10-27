namespace Bit.Seeder.Data.Generators;

/// <summary>
/// Mixes a seed and an item index into a random stream for that item, using the SplitMix64 function, so that no two
/// (seed, index) pairs share a stream.
/// </summary>
internal static class ItemSeedMixer
{
    /// <summary>
    /// Builds the seed for a <c>Randomizer</c> that serves the item at <paramref name="index"/>.
    /// </summary>
    /// <remarks>
    /// Don't use <c>seed + index</c>: it gives (seed s, index i + 1) and (seed s + 1, index i) the same stream, so a
    /// second org at a nearby seed repeats the first org's items shifted by one. <c>System.Random</c> seeded with
    /// consecutive values also correlates its first draws, so neighbouring indices would look alike.
    /// </remarks>
    internal static int Mix(int seed, int index) => unchecked((int)StartStream(seed, index));

    /// <summary>
    /// Builds the starting state of the random stream for the item at <paramref name="index"/>.
    /// </summary>
    internal static ulong StartStream(int seed, int index)
    {
        var pair = ((ulong)(uint)seed << 32) | (uint)index;
        return NextValue(ref pair);
    }

    /// <summary>
    /// Advances <paramref name="stream"/> and returns its next 64-bit value.
    /// </summary>
    internal static ulong NextValue(ref ulong stream)
    {
        var z = stream += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
