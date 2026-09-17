using CommandDotNet;

namespace Bit.DataBoundaries.Commands;

internal sealed class CheckArgs : IArgumentModel
{
    [Option("changed-files", Description =
        "File of newline-separated repository-relative paths to check. Defaults to the git working tree.")]
    public string? ChangedFiles { get; set; }

    [Option("baseline-allowlist", Description =
        "The grandfathered-tables list as it stands at the base of the change, for the growth ratchet. " +
        "Defaults to the copy at HEAD.")]
    public string? BaselineAllowlist { get; set; }

    [Option("sql-ownership", Description =
        "Require every changed table file to resolve to a product owner")]
    public bool SqlOwnership { get; set; }

    [Option("staleness", Description =
        "Require the committed schema map to match what a fresh scan would write")]
    public bool Staleness { get; set; }

    [Option("repo-root", Description = "Repository root (default: walk up from the current directory for a .git entry)")]
    public string? RepoRoot { get; set; }
}
