using System.Collections.Immutable;

namespace Bit.DataBoundaries.Schema;

internal sealed record SchemaInventory(
    ImmutableArray<SchemaTable> Tables,
    ImmutableArray<SchemaParseError> Errors);
