using Bit.DataBoundaries.Commands;
using CommandDotNet;

namespace Bit.DataBoundaries;

internal sealed class Program
{
    private static int Main(string[] args)
    {
        return new AppRunner<Program>().Run(args);
    }

    [Subcommand]
    public ScanCommand Scan { get; set; } = null!;

    [Subcommand]
    public CheckCommand Check { get; set; } = null!;

    [Subcommand]
    public ViolationsCommand Violations { get; set; } = null!;
}
