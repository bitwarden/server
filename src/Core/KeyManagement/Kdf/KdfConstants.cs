namespace Bit.Core.KeyManagement.Kdf;

/// <summary>
/// These defaults are used by the prelogin enumeration-protection pool in
/// Identity/AccountsController. Changes here will affect prelogin responses
/// for non-existent users. See AccountsControllerTests for coverage.
/// </summary>
public static class KdfConstants
{
    public static readonly RangeConstant PBKDF2_ITERATIONS = new(600_000, 2_000_000, 600_000);

    public static readonly RangeConstant ARGON2_ITERATIONS = new(2, 10, 6);
    public static readonly RangeConstant ARGON2_MEMORY = new(15, 1024, 32);
    public static readonly RangeConstant ARGON2_PARALLELISM = new(1, 16, 4);
}

public class RangeConstant
{
    public int Default { get; }
    public int Min { get; }
    public int Max { get; }

    public RangeConstant(int min, int max, int defaultValue)
    {
        Default = defaultValue;
        Min = min;
        Max = max;

        if (Min > Max)
        {
            throw new ArgumentOutOfRangeException($"{Min} is larger than {Max}.");
        }

        if (!InsideRange(defaultValue))
        {
            throw new ArgumentOutOfRangeException($"{Default} is outside allowed range of {Min}-{Max}.");
        }
    }

    public bool InsideRange(int number)
    {
        return Min <= number && number <= Max;
    }
}
