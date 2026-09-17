using CommandDotNet;

namespace Bit.DataBoundaries.Commands;

internal sealed class RoslynSmokeArgs : IArgumentModel
{
    [Option("repo-root", Description = "Repository root (default: walk up from the current directory for a .git entry)")]
    public string? RepoRoot { get; set; }

    [Option("max-diagnostics", Description = "How many workspace load notes to print")]
    public int MaxDiagnostics { get; set; } = 10;

    [Option("max-writes", Description = "How many write accesses to list")]
    public int MaxWrites { get; set; } = 40;

    [Option("max-helpers", Description = "How many entity helper methods to list")]
    public int MaxHelpers { get; set; } = 25;

    [Option("max-reads", Description = "How many read accesses to list")]
    public int MaxReads { get; set; } = 40;

    [Option("type", Description = "Fully qualified entity metadata name to analyze (default: the Organization oracle)")]
    public string? Type { get; set; }

    [Option("property", Description = "Column to use as the oracle for the pass/fail checks (default: MaxStorageGb)")]
    public string? Property { get; set; }

    [Option("expected-consumer", Description = "Repo-relative file expected to read the oracle column (default: CipherService.cs)")]
    public string? ExpectedConsumer { get; set; }
}
