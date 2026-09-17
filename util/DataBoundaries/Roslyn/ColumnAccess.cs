namespace Bit.DataBoundaries.Roslyn;

/// <summary>
/// One access to one column, at one place in the source. <see cref="ViaHelper"/> is null for a
/// direct access and otherwise names the entity helper method the consumer called, so an
/// attribution can be audited rather than taken on trust.
/// </summary>
internal sealed record ColumnAccess(
    string Column,
    string File,
    int Line,
    AccessKind Kind,
    string? ViaHelper)
{
    public bool IsIndirect => ViaHelper is not null;
}
