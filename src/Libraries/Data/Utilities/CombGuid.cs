namespace Bit.Core.Utilities;

/// <summary>
/// Generates sequential ("comb") <see cref="Guid"/> values for SQL Server. Embedding timestamp information makes
/// the IDs sort roughly in creation order, which prevents SQL Server index fragmentation. Prefer
/// <see cref="Generate()"/> over <see cref="Guid.NewGuid"/> for any database IDs.
/// </summary>
/// <remarks>
/// ref: https://github.com/nhibernate/nhibernate-core/blob/master/src/NHibernate/Id/GuidCombGenerator.cs
/// </remarks>
public static class
    CombGuid
{
    private static readonly long _baseDateTicks = new DateTime(1900, 1, 1).Ticks;

    /// <summary>
    /// Generate a sequential comb <see cref="Guid"/> for the current instant.
    /// </summary>
    /// <returns>A comb Guid.</returns>
    public static Guid Generate()
        => Generate(Guid.NewGuid(), DateTime.UtcNow);

    /// <summary>
    /// Implementation of <see cref="Generate()" /> with input parameters to remove randomness.
    /// This should NOT be used outside of testing.
    /// </summary>
    /// <remarks>
    /// You probably don't want to use this method and instead want to use <see cref="Generate()" /> with no parameters
    /// </remarks>
    internal static Guid Generate(Guid startingGuid, DateTime time)
    {
        var guidArray = startingGuid.ToByteArray();

        // Get the days and milliseconds which will be used to build the byte string
        var days = new TimeSpan(time.Ticks - _baseDateTicks);
        var msecs = time.TimeOfDay;

        // Convert to a byte array
        // Note that SQL Server is accurate to 1/300th of a millisecond so we divide by 3.333333
        var daysArray = BitConverter.GetBytes(days.Days);
        var msecsArray = BitConverter.GetBytes((long)(msecs.TotalMilliseconds / 3.333333));

        // Reverse the bytes to match SQL Servers ordering
        Array.Reverse(daysArray);
        Array.Reverse(msecsArray);

        // Copy the bytes into the guid
        Array.Copy(daysArray, daysArray.Length - 2, guidArray, guidArray.Length - 6, 2);
        Array.Copy(msecsArray, msecsArray.Length - 4, guidArray, guidArray.Length - 4, 4);

        return new Guid(guidArray);
    }
}
