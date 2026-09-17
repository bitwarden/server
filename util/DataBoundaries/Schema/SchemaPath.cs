namespace Bit.DataBoundaries.Schema;

/// <summary>
/// Classifies repository-relative paths against the SSDT schema layout.
/// </summary>
internal static class SchemaPath
{
    private static readonly string _schemaRootPrefix = SchemaReader.SchemaRoot + "/";
    private static readonly string _unclassifiedPrefix = SchemaReader.SchemaRoot + "/Tables/";

    /// <summary>
    /// A table file under the schema root, with or without a domain folder above Tables.
    /// </summary>
    public static bool IsTableFile(string repoRelativePath) =>
        repoRelativePath.StartsWith(_schemaRootPrefix, StringComparison.Ordinal)
        && repoRelativePath.EndsWith(".sql", StringComparison.Ordinal)
        && Path.GetDirectoryName(repoRelativePath)?.Replace('\\', '/') is { } directory
        && directory.EndsWith("/Tables", StringComparison.Ordinal);

    /// <summary>
    /// A table file sitting directly in src/Sql/dbo/Tables, where no domain folder claims it and
    /// CODEOWNERS therefore resolves it to the database platform team rather than a product team.
    /// </summary>
    public static bool IsUnclassifiedTableFile(string repoRelativePath) =>
        IsTableFile(repoRelativePath)
        && repoRelativePath.StartsWith(_unclassifiedPrefix, StringComparison.Ordinal);
}
