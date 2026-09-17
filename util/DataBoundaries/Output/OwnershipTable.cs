using System.Collections.Immutable;

namespace Bit.DataBoundaries.Output;

/// <param name="SchemaOwnersLine">
/// Line in .github/CODEOWNERS that decided the owners, or null when no pattern matched. Last-match-wins
/// means several patterns can match one file, so the line is what makes an attribution checkable.
/// </param>
internal sealed record OwnershipTable(
    string Schema,
    string Table,
    string SchemaFile,
    ImmutableArray<string> SchemaOwners,
    int? SchemaOwnersLine,
    string? SchemaDomain,
    int ColumnCount,
    ImmutableArray<string> Columns);
