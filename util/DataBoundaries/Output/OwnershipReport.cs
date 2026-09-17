using System.Collections.Immutable;
using Bit.DataBoundaries.Domains;
using Bit.DataBoundaries.Ownership;
using Bit.DataBoundaries.Schema;

namespace Bit.DataBoundaries.Output;

internal sealed record OwnershipReport(string CsharpAnalysis, ImmutableArray<OwnershipTable> Tables)
{
    public const string FileName = "schema-inventory.json";

    /// <summary>
    /// Marks the report as a schema-and-ownership map only, with no C# read/write tracing behind it.
    /// </summary>
    private const string _csharpAnalysisNotPerformed = "not-performed-stage-1";

    private static readonly string _schemaRootPrefix = SchemaReader.SchemaRoot + "/";

    public static OwnershipReport Build(
        IEnumerable<SchemaTable> tables,
        CodeownersResolver codeowners,
        DomainResolver domains) =>
        new(_csharpAnalysisNotPerformed,
        [
            .. tables
                .Select(table => Map(table, codeowners, domains))
                .OrderBy(row => row.Table, StringComparer.Ordinal)
                .ThenBy(row => row.Schema, StringComparer.Ordinal)
                .ThenBy(row => row.SchemaFile, StringComparer.Ordinal)
        ]);

    private static OwnershipTable Map(SchemaTable table, CodeownersResolver codeowners, DomainResolver domains)
    {
        var rule = codeowners.ResolveRule(table.SchemaFile);
        var owners = rule?.Owners ?? ImmutableArray<string>.Empty;
        var domain = SchemaFolderDomain(table.SchemaFile, domains);

        return new OwnershipTable(
            table.Schema,
            table.Table,
            table.SchemaFile,
            [.. owners.OrderBy(owner => owner, StringComparer.Ordinal)],
            rule?.LineNumber,
            domain?.Domain,
            table.Columns.Length,
            [.. table.Columns.OrderBy(column => column, StringComparer.Ordinal)]);
    }

    private static DomainAssignment? SchemaFolderDomain(string schemaFile, DomainResolver domains)
    {
        if (!schemaFile.StartsWith(_schemaRootPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        // src/Sql/dbo/<Domain>/Tables/<file>.sql carries a domain folder; src/Sql/dbo/Tables does not.
        var segments = schemaFile[_schemaRootPrefix.Length..].Split('/');
        return segments.Length < 3 ? null : domains.ResolveSegment(segments[0]);
    }
}
