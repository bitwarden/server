using Bit.RestrictedDependencyBaselines.Analysis;
using Bit.RestrictedDependencyBaselines.Baselines;
using Bit.RestrictedDependencyBaselines.Configuration;

namespace Bit.RestrictedDependencyBaselines;

/// <summary>
/// Entry point: <c>dotnet run --project util/RestrictedDependencies/RestrictedDependencyBaselines -- update [--prune] [--repo-root &lt;dir&gt;]</c>.
/// Analyzes the solution and rewrites util/RestrictedDependencies/baselines/ to match the code.
/// </summary>
public static class Program
{
    private const string Usage = "usage: update [--prune] [--repo-root <dir>]";

    private static async Task<int> Main(string[] args)
    {
        // Before anything else: MSBuildLocator has to run before the first MSBuild type loads or
        // the workspace fails once it opens.
        SolutionLoader.RegisterMsBuild();

        if (args.Length == 0 || args[0] != "update")
        {
            Console.Error.WriteLine(Usage);
            return ExitCode.ToolFailure;
        }

        var prune = false;
        string? explicitRoot = null;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--prune":
                    prune = true;
                    break;
                case "--repo-root" when i + 1 < args.Length:
                    explicitRoot = args[++i];
                    break;
                default:
                    Console.Error.WriteLine(Usage);
                    return ExitCode.ToolFailure;
            }
        }

        try
        {
            return await UpdateAsync(explicitRoot, prune);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            return ExitCode.ToolFailure;
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or ArgumentException)
        {
            Console.Error.WriteLine(e.Message);
            return ExitCode.ToolFailure;
        }
    }

    private static async Task<int> UpdateAsync(string? explicitRoot, bool prune)
    {
        var repoRoot = RepoLocator.Resolve(explicitRoot, Environment.CurrentDirectory);

        // Ctrl+C cancels the scan instead of killing the process part-way through writing a baseline.
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        var analysis = await ToolAnalysisRunner.AnalyzeSolutionAsync(repoRoot, Console.Out, cancellation.Token);
        BaselineWriter.Write(repoRoot, BaselineBuilder.Build(analysis), prune, Console.Out);

        PrintIfAny("warning: the analysis may have missed uses:", analysis.Degradations);

        if (analysis.AnalyzerFailures.IsEmpty)
        {
            return ExitCode.Clean;
        }

        // The baselines are written anyway so the partial result can be inspected, but the exit
        // code has to say the tool failed: an analyzer that threw did not record the uses the
        // crashed action was responsible for.
        PrintIfAny("error: the analyzer threw; the baselines above are incomplete:", analysis.AnalyzerFailures);
        return ExitCode.ToolFailure;
    }

    private static void PrintIfAny(string heading, IEnumerable<string> lines)
    {
        var list = lines.ToList();
        if (list.Count == 0)
        {
            return;
        }

        Console.Error.WriteLine(heading);
        foreach (var line in list)
        {
            Console.Error.WriteLine($"  - {line}");
        }
    }
}
