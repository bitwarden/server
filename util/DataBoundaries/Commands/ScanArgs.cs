using CommandDotNet;

namespace Bit.DataBoundaries.Commands;

internal sealed class ScanArgs : IArgumentModel
{
    public const string DefaultOutDirectory = "util/DataBoundaries/generated";

    [Option("table", Description = "Limit the report to one table, matched case-insensitively by name")]
    public string? Table { get; set; }

    [Option("out", Description = "Directory for generated files, relative to the repository root and required to stay inside it")]
    public string OutDirectory { get; set; } = DefaultOutDirectory;

    [Option("repo-root", Description = "Repository root (default: walk up from the current directory for a .git entry)")]
    public string? RepoRoot { get; set; }
}
