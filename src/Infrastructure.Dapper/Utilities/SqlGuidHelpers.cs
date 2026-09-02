using System.Data.SqlTypes;

namespace Bit.Infrastructure.Dapper.Utilities;

public static class SqlGuidHelpers
{
    /// <summary>
    /// Sorts the source IEnumerable by the specified Guid property using the <see cref="SqlGuid"/> comparison logic.
    /// This is required because MSSQL server compares (and therefore sorts) Guids differently to C#.
    /// Ref: https://learn.microsoft.com/en-us/sql/connect/ado-net/sql/compare-guid-uniqueidentifier-values
    /// </summary>
    public static IOrderedEnumerable<T> OrderBySqlGuid<T>(
        this IEnumerable<T> source,
        Func<T, Guid> keySelector)
    {
        return source.OrderBy(x => new SqlGuid(keySelector(x)));
    }

    /// <inheritdoc cref="OrderBySqlGuid"/>
    public static IOrderedEnumerable<T> ThenBySqlGuid<T>(
        this IOrderedEnumerable<T> source,
        Func<T, Guid> keySelector)
    {
        return source.ThenBy(x => new SqlGuid(keySelector(x)));
    }
}
