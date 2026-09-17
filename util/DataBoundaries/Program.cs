using Bit.DataBoundaries.Commands;
using Bit.DataBoundaries.Roslyn;
using CommandDotNet;

namespace Bit.DataBoundaries;

internal sealed class Program
{
    private static int Main(string[] args)
    {
        // MSBuildLocator has to run before any MSBuild type loads, so it goes here rather than in
        // the command that needs it. It only locates the SDK, so verbs that never touch Roslyn are
        // unaffected.
        SolutionLoader.RegisterMsBuild();
        return new AppRunner<Program>().Run(args);
    }

    [Subcommand]
    public ScanCommand Scan { get; set; } = null!;

    [Subcommand]
    public CheckCommand Check { get; set; } = null!;

    [Subcommand]
    public RoslynSmokeCommand RoslynSmoke { get; set; } = null!;

    [Subcommand]
    public ViolationsCommand Violations { get; set; } = null!;
}
