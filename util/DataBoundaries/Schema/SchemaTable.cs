using System.Collections.Immutable;

namespace Bit.DataBoundaries.Schema;

/// <param name="SchemaFile">Repository-relative path with forward slashes.</param>
/// <param name="Columns">Column names in the order they are declared in the DDL.</param>
internal sealed record SchemaTable(
    string Schema,
    string Table,
    string SchemaFile,
    ImmutableArray<string> Columns);
