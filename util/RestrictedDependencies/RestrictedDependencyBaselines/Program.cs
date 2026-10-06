using Bit.RestrictedDependencyBaselines.Analysis;
using Bit.RestrictedDependencyBaselines.Baselines;
using Bit.RestrictedDependencyBaselines.Configuration;
using Bitwarden.Server.Sdk.RestrictedDependencies;

namespace Bit.RestrictedDependencyBaselines;

/// <summary>
/// Entry point: <c>dotnet run --project util/RestrictedDependencies/RestrictedDependencyBaselines -- update [--prune] [--allow-growth] [--repo-root &lt;dir&gt;]</c>.
/// Analyzes the solution and rewrites util/RestrictedDependencies/baselines/ to match the code,
/// unless the result would grow a budget and --allow-growth was not passed.
/// </summary>
public static class Program
{
    private const string Usage = "usage: update [--prune] [--allow-growth] [--repo-root <dir>]";

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
        var allowGrowth = false;
        string? explicitRoot = null;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--prune":
                    prune = true;
                    break;
                case "--allow-growth":
                    allowGrowth = true;
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
            return await UpdateAsync(explicitRoot, prune, allowGrowth);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            return ExitCode.ToolFailure;
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or ArgumentException or FormatException)
        {
            Console.Error.WriteLine(e.Message);
            return ExitCode.ToolFailure;
        }
    }

    private static async Task<int> UpdateAsync(string? explicitRoot, bool prune, bool allowGrowth)
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
        var regenerated = BaselineBuilder.Build(analysis);

        // Moves between sites net to zero and are written; raising a budget has to be asked for.
        var growth = BudgetRatchet.FindGrowth(ReadOnDisk(repoRoot), regenerated);
        if (growth.Count > 0 && !allowGrowth)
        {
            PrintIfAny("error: the regenerated baselines grow a budget, so nothing was written. Remove the new uses, or re-run with --allow-growth if the growth is intended:", growth);
            return ExitCode.GrowthRefused;
        }

        BaselineWriter.Write(repoRoot, regenerated, prune, Console.Out);

        PrintIfAny("warning: --allow-growth wrote budget growth:", growth);
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

    private static List<BudgetModel> ReadOnDisk(string repoRoot)
    {
        var directory = Path.Combine(repoRoot, RepoLocator.BaselinesDirectory);
        return Directory.Exists(directory)
            ? [.. Directory.EnumerateFiles(directory, "*.json").Select(path => BudgetModel.Parse(File.ReadAllText(path)))]
            : [];
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
