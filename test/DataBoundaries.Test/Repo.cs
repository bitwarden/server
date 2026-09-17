using Bit.DataBoundaries.Schema;

namespace Bit.DataBoundaries.Test;

/// <summary>
/// The real artifacts in this repository. These tests are intentionally coupled to the committed
/// schema and CODEOWNERS so that ownership drift shows up as a failing test.
/// </summary>
internal static class Repo
{
    public static string Root { get; } = RepoLocator.Resolve(null, AppContext.BaseDirectory);

    public static SchemaInventory Schema { get; } = SchemaReader.Read(Root);

    public static SchemaTable Table(string name) => Schema.Tables.Single(t => t.Table == name);
}
