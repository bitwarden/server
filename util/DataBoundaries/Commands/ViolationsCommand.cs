using CommandDotNet;

namespace Bit.DataBoundaries.Commands;

[Command("violations", Description = "List columns whose C# usage conflicts with schema ownership. Requires the Roslyn analysis stage.")]
internal sealed class ViolationsCommand
{
    [DefaultCommand]
    public int Execute()
    {
        Console.Error.WriteLine(
            "'violations' requires the Roslyn semantic analysis stage, which is not implemented yet. " +
            "It cannot be answered from schema and CODEOWNERS data alone, so this run produced no verdict. " +
            "'scan' and 'check' are available today.");
        return ExitCode.ToolFailure;
    }
}
