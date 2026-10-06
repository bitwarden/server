using System.Globalization;
using Bit.DataMigrations;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.DataMigrations;

public class DataMigrationSqlTests
{
    private static DataMigrationSql Sql(bool sqlServer) => new(identifier => $"[{identifier}]", sqlServer, "T", "Id", "A");

    [Fact]
    public void UpdateFromJson_TypesTheColumnFromTheTable()
    {
        Assert.Equal(
            "UPDATE t SET [A] = j.n FROM [T] t JOIN OPENJSON({0}) WITH (k uniqueidentifier '$.k', n varchar(30) '$.n', " +
            "o varchar(30) '$.o') j ON t.[Id] = j.k AND (t.[A] = j.o OR (t.[A] IS NULL AND j.o IS NULL))",
            Sql(sqlServer: true).UpdateFromJson("varchar(30)"));
    }

    [Theory]
    [InlineData("a", "b", "UPDATE [T] SET [A] = {1} WHERE [Id] = {0} AND [A] = {2}")]
    [InlineData(null, "b", "UPDATE [T] SET [A] = {1} WHERE [Id] = {0} AND [A] IS NULL")]
    [InlineData("a", null, "UPDATE [T] SET [A] = NULL WHERE [Id] = {0} AND [A] = {1}")]
    public void UpdateRow_MatchesAndWritesNullsAsLiterals(string? before, string? after, string expected)
    {
        var (sql, args) = Sql(sqlServer: false).UpdateRow(Guid.Empty, before, after);

        Assert.Equal(expected, sql);
        Assert.Equal(new object?[] { Guid.Empty, after, before }.OfType<object>(), args);
    }

    [Fact]
    public void Select_ReadsFromTheCursorThroughTheRangeEndPerProvider()
    {
        Assert.Equal("SELECT TOP (5) [Id] AS [Id], [A] AS [Value] FROM [T] WHERE [Id] >= {0} AND [Id] <= {1} ORDER BY [Id]",
            Sql(sqlServer: true).Select(5));
        Assert.Equal("SELECT [Id] AS [Id], [A] AS [Value] FROM [T] WHERE [Id] >= {0} AND [Id] <= {1} ORDER BY [Id] LIMIT 5",
            Sql(sqlServer: false).Select(5));
    }

    [Theory]
    [InlineData(10, 3, new long[] { 4, 4, 2 })]
    [InlineData(9, 3, new long[] { 3, 3, 3 })]
    [InlineData(13, 3, new long[] { 5, 5, 3 })]
    [InlineData(2, 3, new long[] { 2 })]
    [InlineData(1, 1, new long[] { 1 })]
    public void Partitions_CoverEveryRowOnce(int count, int partitions, long[] sizes)
    {
        var step = DataMigrationSql.Step(count, partitions);
        // What Bounds returns for keys 1..count.
        var bounds = Enumerable.Range(1, count).Where(rn => rn % step is 0 or 1 || rn == count).Select(rn => $"{rn}").ToList();

        var result = DataMigrationSql.Partitions("M", count, step, bounds);

        var ranges = result.Select(p => Enumerable.Range(1, count).Where(k => k >= Key(p.RangeStart) && k <= Key(p.RangeEnd))).ToList();
        Assert.Equal(sizes, result.Select(p => p.TotalRows));
        Assert.Equal(sizes, ranges.Select(r => (long)r.Count()));
        Assert.Equal(Enumerable.Range(1, count), ranges.SelectMany(r => r));
        Assert.All(result, p => Assert.Equal(p.RangeStart, p.Cursor));
    }

    private static int Key(string key) => int.Parse(key, CultureInfo.InvariantCulture);
}
