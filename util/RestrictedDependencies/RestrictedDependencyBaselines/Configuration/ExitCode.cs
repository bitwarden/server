namespace Bit.RestrictedDependencyBaselines.Configuration;

/// <summary>
/// Process exit codes: clean, or the tool itself failed.
/// </summary>
internal static class ExitCode
{
    public const int Clean = 0;
    public const int ToolFailure = 2;
}
