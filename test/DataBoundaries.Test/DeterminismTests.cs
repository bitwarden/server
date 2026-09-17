using System.Collections.Immutable;
using Bit.DataBoundaries.Output;
using Bit.DataBoundaries.Schema;

namespace Bit.DataBoundaries.Test;

public class DeterminismTests
{
    [Fact]
    public void Write_ProducesByteIdenticalFiles_RegardlessOfInputOrdering()
    {
        var directory = Directory.CreateTempSubdirectory("data-boundaries-determinism");
        try
        {
            var first = Path.Combine(directory.FullName, "first.json");
            var second = Path.Combine(directory.FullName, "second.json");

            ReportWriter.Write(first, ReportWriter.Serialize(BuildFrom(Shuffled(seed: 1))));
            ReportWriter.Write(second, ReportWriter.Serialize(BuildFrom(Shuffled(seed: 2))));

            Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Serialize_UsesLineFeedEndingsOnly()
    {
        var json = ReportWriter.Serialize(BuildFrom(Repo.Schema.Tables));

        Assert.DoesNotContain('\r', json);
    }

    [Fact]
    public void Serialize_EndsWithASingleTrailingNewline()
    {
        var json = ReportWriter.Serialize(BuildFrom(Repo.Schema.Tables));

        Assert.EndsWith("}\n", json);
    }

    [Fact]
    public void Serialize_MarksCsharpAnalysisAsNotPerformed()
    {
        var json = ReportWriter.Serialize(BuildFrom(Repo.Schema.Tables));

        Assert.Contains("\"csharpAnalysis\": \"not-performed-stage-1\"", json);
    }

    [Fact]
    public void Build_SortsTablesOrdinally()
    {
        var tables = BuildFrom(Shuffled(seed: 3)).Tables.Select(t => t.Table).ToArray();

        Assert.Equal(tables.Order(StringComparer.Ordinal), tables);
    }

    [Fact]
    public void Build_SortsColumnsOrdinally()
    {
        var columns = BuildFrom(Shuffled(seed: 4)).Tables
            .Single(t => t.Table == "Organization").Columns;

        Assert.Equal(columns.Order(StringComparer.Ordinal), columns);
    }

    [Fact]
    public void Serialize_ContainsNoAbsolutePaths()
    {
        var json = ReportWriter.Serialize(BuildFrom(Repo.Schema.Tables));

        Assert.DoesNotContain(Repo.Root, json, StringComparison.Ordinal);
    }

    private static OwnershipReport BuildFrom(IEnumerable<SchemaTable> tables) =>
        OwnershipReport.Build(tables, Repo.Codeowners, Repo.Domains);

    private static ImmutableArray<SchemaTable> Shuffled(int seed)
    {
        var random = new Random(seed);
        return
        [
            .. Repo.Schema.Tables
                .Select(t => t with { Columns = [.. t.Columns.OrderBy(_ => random.Next())] })
                .OrderBy(_ => random.Next())
        ];
    }
}
