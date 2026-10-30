namespace Bit.RestrictedDependencyBaselines.Configuration;

/// <summary>
/// Process exit codes: clean, growth refused, or the tool itself failed.
/// </summary>
internal static class ExitCode
{
    public const int Clean = 0;
    public const int GrowthRefused = 1;
    public const int ToolFailure = 2;
}
