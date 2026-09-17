using System.Collections.Immutable;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Bit.DataBoundaries.Schema;

internal static class SchemaReader
{
    public const string SchemaRoot = "src/Sql/dbo";

    private const string TablesDirectoryName = "Tables";

    public static SchemaInventory Read(string repoRoot)
    {
        var root = Path.Combine(repoRoot, "src", "Sql", "dbo");
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Schema root not found: {Path.Combine(repoRoot, SchemaRoot)}");
        }

        var tables = ImmutableArray.CreateBuilder<SchemaTable>();
        var errors = ImmutableArray.CreateBuilder<SchemaParseError>();

        foreach (var file in EnumerateTableFiles(root))
        {
            ReadFile(file, ToRepoRelative(repoRoot, file), tables, errors);
        }

        return new SchemaInventory(tables.ToImmutable(), errors.ToImmutable());
    }

    private static ImmutableArray<string> EnumerateTableFiles(string schemaRoot) =>
    [
        .. Directory.EnumerateFiles(schemaRoot, "*.sql", SearchOption.AllDirectories)
            .Where(IsInTablesDirectory)
            .OrderBy(f => f, StringComparer.Ordinal)
    ];

    private static bool IsInTablesDirectory(string file) =>
        Path.GetDirectoryName(file) is { } directory && Path.GetFileName(directory) == TablesDirectoryName;

    private static void ReadFile(
        string fullPath,
        string repoRelativePath,
        ImmutableArray<SchemaTable>.Builder tables,
        ImmutableArray<SchemaParseError>.Builder errors)
    {
        var parser = new TSql180Parser(initialQuotedIdentifiers: true);

        TSqlFragment fragment;
        IList<ParseError> parseErrors;
        using (var reader = new StreamReader(fullPath))
        {
            fragment = parser.Parse(reader, out parseErrors);
        }

        if (parseErrors.Count > 0)
        {
            foreach (var parseError in parseErrors)
            {
                errors.Add(new SchemaParseError(
                    repoRelativePath, parseError.Line, parseError.Column, parseError.Message));
            }

            return;
        }

        var visitor = new CreateTableVisitor();
        fragment.Accept(visitor);

        foreach (var statement in visitor.Statements)
        {
            var name = statement.SchemaObjectName;
            if (name?.BaseIdentifier?.Value is not { Length: > 0 } table)
            {
                continue;
            }

            var schema = name.SchemaIdentifier?.Value is { Length: > 0 } declared ? declared : "dbo";
            var columns = statement.Definition is null
                ? ImmutableArray<string>.Empty
                : [.. statement.Definition.ColumnDefinitions
                    .Select(c => c.ColumnIdentifier?.Value)
                    .Where(v => v is { Length: > 0 })
                    .Select(v => v!)];

            tables.Add(new SchemaTable(schema, table, repoRelativePath, columns));
        }
    }

    private static string ToRepoRelative(string repoRoot, string fullPath) =>
        Path.GetRelativePath(repoRoot, fullPath).Replace('\\', '/');

    private sealed class CreateTableVisitor : TSqlFragmentVisitor
    {
        public List<CreateTableStatement> Statements { get; } = [];

        public override void Visit(CreateTableStatement node) => Statements.Add(node);
    }
}
