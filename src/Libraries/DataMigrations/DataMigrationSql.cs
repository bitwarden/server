namespace Bit.DataMigrations;

/// <summary>
/// The SQL a <see cref="DataMigration{TValue}"/> runs against its table, with identifiers quoted for the provider and
/// values passed as EF's <c>{n}</c> parameters. Examples show SQL Server's output for table <c>T</c>, key <c>Id</c> and
/// column <c>A</c>.
/// </summary>
internal class DataMigrationSql(Func<string, string> quote, bool sqlServer, string table, string key, string column)
{
    // OPENJSON silently truncates values to a declared length, so string and binary columns are declared (max) and the
    // UPDATE itself rejects a value too long for its column.
    internal const string ColumnType =
        """
        SELECT CASE
            WHEN CHARACTER_MAXIMUM_LENGTH IS NOT NULL THEN CASE DATA_TYPE
                WHEN 'char' THEN 'varchar' WHEN 'nchar' THEN 'nvarchar' WHEN 'binary' THEN 'varbinary' ELSE DATA_TYPE END + '(max)'
            WHEN DATA_TYPE IN ('decimal', 'numeric') THEN DATA_TYPE + '(' + CAST(NUMERIC_PRECISION AS VARCHAR(10)) + ',' + CAST(NUMERIC_SCALE AS VARCHAR(10)) + ')'
            WHEN DATA_TYPE IN ('datetime2', 'datetimeoffset', 'time') THEN DATA_TYPE + '(' + CAST(DATETIME_PRECISION AS VARCHAR(10)) + ')'
            ELSE DATA_TYPE END AS Value
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = SCHEMA_NAME() AND TABLE_NAME = {0} AND COLUMN_NAME = {1}
        """;

    /// <summary>
    /// Rows per partition. <see cref="Bounds"/> returns each partition's first and last row in pairs, so it's at least two.
    /// </summary>
    internal static long Step(long count, int partitionCount) => Math.Max(2, (count + partitionCount - 1) / partitionCount);

    /// <summary>Partitions from the keys <see cref="Bounds"/> returns; the last partition's pair is one key if it has one row.</summary>
    internal static List<DataMigrationState> Partitions(string name, long count, long step, IReadOnlyList<string> bounds) =>
        bounds.Chunk(2).Select((range, i) => new DataMigrationState
        {
            Name = name,
            Partition = i,
            RangeStart = range[0],
            RangeEnd = range[^1],
            Cursor = range[0],
            TotalRows = i == (bounds.Count - 1) / 2 ? count - step * i : step,
        }).ToList();

    private string Table => field ??= quote(table);
    private string Key => field ??= quote(key);
    private string Column => field ??= quote(column);
    private string SelectList => field ??= $"{Key} AS {quote("Id")}, {Column} AS {quote("Value")}";

    /// <example>SELECT COUNT_BIG(*) AS [Value] FROM [T]</example>
    internal string Count => $"SELECT {(sqlServer ? "COUNT_BIG" : "COUNT")}(*) AS {quote("Value")} FROM {Table}";

    /// <summary>
    /// The key of the first and last row of every <paramref name="step"/> rows, numbered and counted in one statement so
    /// partitions cover every row that exists when it runs.
    /// </summary>
    /// <example>
    /// SELECT [Id] AS [Value] FROM (SELECT [Id], ROW_NUMBER() OVER (ORDER BY [Id]) AS rn, COUNT(*) OVER () AS n FROM [T]) x
    /// WHERE rn % 4 IN (0, 1) OR rn = n ORDER BY rn
    /// </example>
    internal string Bounds(long step) =>
        $"SELECT {Key} AS {quote("Value")} FROM (SELECT {Key}, ROW_NUMBER() OVER (ORDER BY {Key}) AS rn, COUNT(*) OVER () AS n " +
        $"FROM {Table}) x WHERE rn % {step} IN (0, 1) OR rn = n ORDER BY rn";

    /// <example>SELECT TOP (1001) [Id] AS [Id], [A] AS [Value] FROM [T] WHERE [Id] >= {0} AND [Id] &lt;= {1} ORDER BY [Id]</example>
    internal string Select(int take) => sqlServer
        ? $"SELECT TOP ({take}) {SelectList} FROM {Table} WHERE {Key} >= {{0}} AND {Key} <= {{1}} ORDER BY {Key}"
        : $"SELECT {SelectList} FROM {Table} WHERE {Key} >= {{0}} AND {Key} <= {{1}} ORDER BY {Key} LIMIT {take}";

    /// <example>UPDATE [T] SET [A] = {1} WHERE [Id] = {0} AND [A] = {2}, with NULL and IS NULL for null values</example>
    internal (string Sql, object[] Args) UpdateRow(Guid id, object? before, object? after)
    {
        var args = new List<object> { id };
        string Param(object? value)
        {
            if (value is null)
            {
                return "NULL";
            }

            args.Add(value);
            return $"{{{args.Count - 1}}}";
        }

        var set = Param(after);
        var cas = before is null ? "IS NULL" : $"= {Param(before)}";
        return ($"UPDATE {Table} SET {Column} = {set} WHERE {Key} = {{0}} AND {Column} {cas}", args.ToArray());
    }

    /// <example>
    /// UPDATE t SET [A] = j.n FROM [T] t JOIN OPENJSON({0}) WITH (k uniqueidentifier '$.k', n varchar(max) '$.n',
    /// o varchar(max) '$.o') j ON t.[Id] = j.k AND (t.[A] = j.o OR (t.[A] IS NULL AND j.o IS NULL))
    /// </example>
    internal string UpdateFromJson(string type) =>
        $"UPDATE t SET {Column} = j.n FROM {Table} t JOIN OPENJSON({{0}}) WITH (k uniqueidentifier '$.k', n {type} '$.n', " +
        $"o {type} '$.o') j ON t.{Key} = j.k AND (t.{Column} = j.o OR (t.{Column} IS NULL AND j.o IS NULL))";
}
