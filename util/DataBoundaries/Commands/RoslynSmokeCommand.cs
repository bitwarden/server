using System.Diagnostics;
using Bit.DataBoundaries.Roslyn;
using CommandDotNet;
using Microsoft.CodeAnalysis;

namespace Bit.DataBoundaries.Commands;

/// <summary>
/// Answers the three unknowns that gate the Roslyn stage: whether MSBuildWorkspace can open a
/// .slnx solution, whether this repository's projects load, and how long that takes. It also
/// resolves one known column read end to end, so a load that "succeeds" but yields no symbols
/// cannot be mistaken for success.
///
/// This verb is scaffolding for the semantic passes and is expected to be replaced by them.
/// </summary>
[Command("roslyn-smoke", Description = "Prove Roslyn can load this solution and resolve a known column read. Opening the solution rewrites unrelated packages.lock.json files; discard those before committing.")]
internal sealed class RoslynSmokeCommand
{
    /// <summary>
    /// A column with direct qualified reads that were confirmed by hand, so the spike has an
    /// oracle rather than just a non-empty result. This is the default target; --type/--property
    /// point the same walk at a different entity, in which case the oracle/cascade/scope checks
    /// below are skipped, since they were written to assert facts about this specific column.
    /// </summary>
    private const string DefaultEntityMetadataName = "Bit.Core.AdminConsole.Entities.Organization";
    private const string DefaultPropertyName = "MaxStorageGb";
    private const string DefaultExpectedConsumer = "src/Core/Vault/Services/Implementations/CipherService.cs";

    [DefaultCommand]
    public async Task<int> Execute(RoslynSmokeArgs args, CancellationToken cancellationToken)
    {
        var entityMetadataName = args.Type ?? DefaultEntityMetadataName;
        var propertyName = args.Property ?? DefaultPropertyName;
        var expectedConsumer = args.ExpectedConsumer ?? DefaultExpectedConsumer;
        var isCustomTarget = args.Type is not null;

        string repoRoot;
        try
        {
            repoRoot = RepoLocator.Resolve(args.RepoRoot, Directory.GetCurrentDirectory());
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            return ExitCode.ToolFailure;
        }

        var stopwatch = Stopwatch.StartNew();
        LoadedSolution loaded;
        try
        {
            loaded = await SolutionLoader.OpenAsync(repoRoot, cancellationToken);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Error: could not open {SolutionLoader.SolutionFileName}: {exception.Message}");
            return ExitCode.ToolFailure;
        }

        using (loaded)
        {
            var openElapsed = stopwatch.Elapsed;
            var analyzable = SolutionLoader.AnalyzableProjects(loaded.Solution);

            Console.WriteLine($"solution:   {SolutionLoader.SolutionFileName} opened in {openElapsed.TotalSeconds:F1}s");
            Console.WriteLine($"projects:   {loaded.Solution.ProjectIds.Count} in solution, {analyzable.Length} analyzable");
            Console.WriteLine($"load notes: {loaded.LoadDiagnostics.Length}");
            foreach (var diagnostic in loaded.LoadDiagnostics.Take(args.MaxDiagnostics))
            {
                Console.WriteLine($"  {Truncate(diagnostic, 200)}");
            }

            var core = analyzable.FirstOrDefault(p => p.Name == "Core");
            if (core is null)
            {
                Console.Error.WriteLine("Error: the Core project did not load, so no entity symbols are reachable.");
                return ExitCode.ToolFailure;
            }

            stopwatch.Restart();
            var compilation = await core.GetCompilationAsync(cancellationToken);
            if (compilation is null)
            {
                Console.Error.WriteLine("Error: Core produced no compilation.");
                return ExitCode.ToolFailure;
            }

            Console.WriteLine($"compile:    Core compiled in {stopwatch.Elapsed.TotalSeconds:F1}s");

            var entity = compilation.GetTypeByMetadataName(entityMetadataName);
            if (entity is null)
            {
                Console.Error.WriteLine(
                    $"Error: could not resolve {entityMetadataName}. The workspace loaded but symbols are not " +
                    "usable, so semantic attribution is not possible in this configuration.");
                return ExitCode.ToolFailure;
            }

            var property = entity.GetMembers(propertyName).OfType<IPropertySymbol>().FirstOrDefault();
            if (property is null)
            {
                Console.Error.WriteLine($"Error: {entityMetadataName} has no property named {propertyName}.");
                return ExitCode.ToolFailure;
            }

            Console.WriteLine($"symbol:     resolved {entity.Name}.{property.Name} ({property.Type})");

            stopwatch.Restart();
            var accesses = await ColumnUsageAnalyzer.AnalyzeAsync(loaded.Solution, entity, repoRoot, cancellationToken);
            Console.WriteLine($"walk:       {accesses.Length} accesses across {accesses.Select(a => a.Column).Distinct().Count()} columns in {stopwatch.Elapsed.TotalSeconds:F1}s");
            Console.WriteLine();

            var writes = accesses.Where(a => a.Kind != AccessKind.Read).ToList();
            Console.WriteLine($"writes:     {writes.Count}");
            foreach (var write in writes.Take(args.MaxWrites))
            {
                Console.WriteLine($"  {write.Kind,-9} {write.Column,-24} {write.File}:{write.Line}");
            }

            var reads = accesses.Where(a => a.Kind == AccessKind.Read).ToList();
            Console.WriteLine();
            Console.WriteLine($"reads:      {reads.Count}");
            foreach (var read in reads.Take(args.MaxReads))
            {
                var via = read.IsIndirect ? $"  via {read.ViaHelper}()" : string.Empty;
                Console.WriteLine($"  {read.Kind,-9} {read.Column,-24} {read.File}:{read.Line}{via}");
            }

            var indirect = accesses.Where(a => a.IsIndirect).ToList();
            Console.WriteLine();
            Console.WriteLine($"indirect:   {indirect.Count} accesses reached through an entity method");
            foreach (var group in indirect
                .GroupBy(a => a.ViaHelper!, StringComparer.Ordinal)
                .OrderByDescending(g => g.Count())
                .Take(args.MaxHelpers))
            {
                var columns = group.Select(a => a.Column).Distinct().OrderBy(c => c, StringComparer.Ordinal);
                Console.WriteLine($"  {group.Key}() -> {string.Join(", ", columns)}  ({group.Select(a => a.File).Distinct().Count()} files)");
            }

            Console.WriteLine();
            Console.WriteLine($"{propertyName} accesses:");
            var target = accesses.Where(a => a.Column == propertyName).ToList();
            foreach (var access in target)
            {
                var via = access.IsIndirect ? $"  via {access.ViaHelper}()" : string.Empty;
                Console.WriteLine($"  {access.Kind,-5} {access.File}:{access.Line}{via}");
            }

            var leakedTests = accesses.Any(a => a.File.StartsWith("test/", StringComparison.Ordinal));

            // The oracle and cascade checks assert facts specific to the default Organization
            // walk (a known consumer, and that User.cs never appears). Neither means anything once
            // --type points somewhere else, so a custom target only gets the scope check.
            if (isCustomTarget)
            {
                Console.WriteLine();
                Console.WriteLine(leakedTests
                    ? "scope:      FAIL, test files appeared in the consumer set"
                    : "scope:      PASS, no test files in the consumer set");
                return leakedTests ? ExitCode.ToolFailure : ExitCode.Clean;
            }

            var wrongTable = accesses.Any(a => a.File.EndsWith("src/Core/Entities/User.cs", StringComparison.Ordinal));
            var hitExpected = target.Any(a => a.File == expectedConsumer);

            Console.WriteLine();
            Console.WriteLine(hitExpected
                ? $"oracle:     PASS, {expectedConsumer} reads {propertyName}"
                : $"oracle:     FAIL, expected {expectedConsumer} to read {propertyName}");
            Console.WriteLine(wrongTable
                ? "cascade:    FAIL, User.cs appeared, so the interface cascade is not contained"
                : "cascade:    PASS, no User.cs hits, so IStorable did not pull in the other table");
            Console.WriteLine(leakedTests
                ? "scope:      FAIL, test files appeared in the consumer set"
                : "scope:      PASS, no test files in the consumer set");

            return hitExpected && !wrongTable && !leakedTests ? ExitCode.Clean : ExitCode.ToolFailure;
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "...";
}
