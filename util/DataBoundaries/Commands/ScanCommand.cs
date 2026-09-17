using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Bit.DataBoundaries.Domains;
using Bit.DataBoundaries.Output;
using Bit.DataBoundaries.Ownership;
using Bit.DataBoundaries.Schema;
using CommandDotNet;

namespace Bit.DataBoundaries.Commands;

[Command("scan", Description =
    "Emit the SQL schema inventory joined with CODEOWNERS ownership and schema-folder domain classification. " +
    "Does not trace C# reads or writes.")]
internal sealed class ScanCommand
{
    private const string CodeownersFile = ".github/CODEOWNERS";

    [DefaultCommand]
    public int Execute(ScanArgs args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            return ExitCode.ToolFailure;
        }
    }

    private static int Run(ScanArgs args)
    {
        var repoRoot = RepoLocator.Resolve(args.RepoRoot, Directory.GetCurrentDirectory());
        var filter = string.IsNullOrWhiteSpace(args.Table) ? null : args.Table;

        var outDirectory = ResolveOutDirectory(repoRoot, args.OutDirectory);
        if (outDirectory is null)
        {
            Console.Error.WriteLine(
                $"Error: --out must stay inside the repository root '{repoRoot}', " +
                $"but '{args.OutDirectory}' resolves outside it.");
            return ExitCode.ToolFailure;
        }

        var inventory = SchemaReader.Read(repoRoot);
        var codeowners = CodeownersResolver.FromFile(Path.Combine(repoRoot, CodeownersFile));
        var domains = DomainResolver.FromRepo(repoRoot);

        if (!inventory.Errors.IsEmpty)
        {
            Console.Error.WriteLine(
                $"Error: {inventory.Errors.Length} T-SQL parse error(s) under {SchemaReader.SchemaRoot}:");
            foreach (var error in inventory.Errors)
            {
                Console.Error.WriteLine($"  {error.SchemaFile}({error.Line},{error.Column}): {error.Message}");
            }

            Console.Error.WriteLine("Nothing was written: a map missing a table would misstate ownership.");
            return ExitCode.ToolFailure;
        }

        var tables = inventory.Tables;
        if (filter is not null)
        {
            tables = [.. tables.Where(t => string.Equals(t.Table, filter, StringComparison.OrdinalIgnoreCase))];
            if (tables.IsEmpty)
            {
                Console.Error.WriteLine(
                    $"Error: no table named '{filter}' under {SchemaReader.SchemaRoot}/**/Tables.");
                return ExitCode.ToolFailure;
            }
        }

        var report = OwnershipReport.Build(tables, codeowners, domains);

        // A --table run inspects one table; it is not a regeneration. Writing its result to the shared
        // path would truncate the committed full map down to that single table.
        string? reportPath = null;
        if (filter is null)
        {
            reportPath = Path.Combine(outDirectory, OwnershipReport.FileName);
            ReportWriter.Write(reportPath, ReportWriter.Serialize(report));
        }

        PrintHeader(report, repoRoot, reportPath);
        PrintCodeownersHygiene(inventory.Tables, codeowners);
        PrintTables(report, listColumns: filter is not null);
        return ExitCode.Clean;
    }

    /// <summary>
    /// Null when the requested directory leaves the repository. Path.Combine discards the repository root
    /// outright when --out is absolute, and '..' segments walk out of it, so the combined path is
    /// canonicalized and checked rather than trusted.
    /// </summary>
    private static string? ResolveOutDirectory(string repoRoot, string outDirectory)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repoRoot));
        var candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(root, outDirectory)));

        var inside = candidate == root
            || candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);

        return inside ? candidate : null;
    }

    private static void PrintHeader(OwnershipReport report, string repoRoot, string? reportPath)
    {
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Tables: {report.Tables.Length} from {SchemaReader.SchemaRoot}/**/Tables"));
        Console.WriteLine($"C# usage analysis: {report.CsharpAnalysis}");
        Console.WriteLine(reportPath is null
            ? "Report: nothing was written. A --table run only inspects; run 'scan' with no --table to " +
              $"regenerate {ScanArgs.DefaultOutDirectory}/{OwnershipReport.FileName}."
            : $"Report: {DescribePath(repoRoot, reportPath)}");
    }

    /// <summary>
    /// Reports drift in the ownership data the map is built from: CODEOWNERS entries that name the schema
    /// root but guard no table file, and table files no entry claims.
    /// </summary>
    private static void PrintCodeownersHygiene(
        ImmutableArray<SchemaTable> allTables,
        CodeownersResolver codeowners)
    {
        var schemaFiles = allTables.Select(table => table.SchemaFile).ToArray();

        Console.WriteLine();
        Console.WriteLine("CODEOWNERS hygiene:");
        PrintList(
            $"patterns naming {SchemaReader.SchemaRoot} that match no table file",
            codeowners.FindUnmatchedPatterns(schemaFiles)
                .Where(pattern => pattern.Contains(SchemaReader.SchemaRoot, StringComparison.Ordinal)));
        PrintList(
            "table files no pattern claims",
            allTables
                .Where(table => codeowners.Resolve(table.SchemaFile).IsEmpty)
                .Select(table => table.SchemaFile)
                .OrderBy(file => file, StringComparer.Ordinal));
    }

    private static void PrintList(string label, IEnumerable<string> values)
    {
        var items = values.ToArray();
        if (items.Length == 0)
        {
            Console.WriteLine($"  {label}: none");
            return;
        }

        Console.WriteLine($"  {label}:");
        foreach (var item in items)
        {
            Console.WriteLine($"    {item}");
        }
    }

    private static void PrintTables(OwnershipReport report, bool listColumns)
    {
        foreach (var table in report.Tables)
        {
            Console.WriteLine();
            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{table.Schema}.{table.Table} ({table.ColumnCount} columns)"));
            Console.WriteLine($"  file:   {table.SchemaFile}");
            Console.WriteLine($"  owners: {DescribeOwners(table)}");
            Console.WriteLine($"  domain: {table.SchemaDomain ?? "(none)"}");

            if (!listColumns)
            {
                continue;
            }

            Console.WriteLine("  columns:");
            foreach (var column in table.Columns)
            {
                Console.WriteLine($"    {column}");
            }
        }
    }

    private static string DescribeOwners(OwnershipTable table)
    {
        var citation = table.SchemaOwnersLine is { } line
            ? string.Create(CultureInfo.InvariantCulture, $" ({CodeownersFile}:{line})")
            : string.Empty;

        return table.SchemaOwners.IsEmpty
            ? $"(none){citation}"
            : $"{string.Join(", ", table.SchemaOwners)}{citation}";
    }

    private static string DescribePath(string repoRoot, string path)
    {
        var relative = Path.GetRelativePath(repoRoot, path).Replace('\\', '/');
        return relative.StartsWith("../", StringComparison.Ordinal) ? path : relative;
    }
}
