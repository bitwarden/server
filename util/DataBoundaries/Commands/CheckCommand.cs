using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Bit.DataBoundaries.Domains;
using Bit.DataBoundaries.Output;
using Bit.DataBoundaries.Ownership;
using Bit.DataBoundaries.Schema;
using CommandDotNet;

namespace Bit.DataBoundaries.Commands;

[Command("check", Description =
    "Fail when a changed table file has no product owner, or when the committed schema map is stale. " +
    "With neither --sql-ownership nor --staleness, both run.")]
internal sealed class CheckCommand
{
    private const string DatabasePlatformOwner = "@bitwarden/dept-dbops";
    private const string RegenerateCommand = "dotnet run --project util/DataBoundaries -- scan";

    private static readonly UTF8Encoding _utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    [DefaultCommand]
    public int Execute(CheckArgs args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            return ExitCode.ToolFailure;
        }
    }

    private static int Run(CheckArgs args)
    {
        var repoRoot = RepoLocator.Resolve(args.RepoRoot, Directory.GetCurrentDirectory());

        // Neither flag means both, so each check runs unless the other was asked for alone.
        var runSqlOwnership = args.SqlOwnership || !args.Staleness;
        var runStaleness = args.Staleness || !args.SqlOwnership;

        var violations = new List<string>();

        if (runSqlOwnership)
        {
            if (ReadChangedFiles(args, repoRoot) is not { } changed)
            {
                return ExitCode.ToolFailure;
            }

            var codeowners = CodeownersResolver.FromFile(Path.Combine(repoRoot, ".github", "CODEOWNERS"));
            var allowlist = GrandfatheredTables.FromRepo(repoRoot);
            var tableFiles = changed.Where(SchemaPath.IsTableFile).ToArray();

            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"sql-ownership: {tableFiles.Length} changed table file(s) of {changed.Length} changed path(s)"));

            CheckTableOwnership(tableFiles, codeowners, allowlist, violations);
            CheckAllowlistGrowth(args, repoRoot, changed, allowlist, violations);
        }

        if (runStaleness)
        {
            Console.WriteLine($"staleness: comparing {ScanArgs.DefaultOutDirectory}/{OwnershipReport.FileName}");
            CheckStaleness(repoRoot, violations);
        }

        return ReportViolations(violations);
    }

    private static void CheckTableOwnership(
        IEnumerable<string> tableFiles,
        CodeownersResolver codeowners,
        IReadOnlySet<string> allowlist,
        List<string> violations)
    {
        foreach (var path in tableFiles)
        {
            var grandfathered = allowlist.Contains(path);

            if (SchemaPath.IsUnclassifiedTableFile(path) && !grandfathered)
            {
                violations.Add(
                    $"{path}: a table file directly in {SchemaReader.SchemaRoot}/Tables that the grandfather " +
                    $"list does not cover, so it is a new unclassified table. Move it to " +
                    $"{SchemaReader.SchemaRoot}/<Domain>/Tables/ so CODEOWNERS resolves it to the team that " +
                    "owns the data.");
                continue;
            }

            if (grandfathered)
            {
                continue;
            }

            var owners = codeowners.Resolve(path);
            if (owners.IsEmpty)
            {
                violations.Add(
                    $"{path}: no CODEOWNERS pattern claims this table file, so no team is accountable for its " +
                    $"columns. Add a rule covering its domain folder, or move it to " +
                    $"{SchemaReader.SchemaRoot}/<Domain>/Tables/.");
                continue;
            }

            if (owners is [DatabasePlatformOwner])
            {
                violations.Add(
                    $"{path}: resolves only to {DatabasePlatformOwner}, which owns the database platform " +
                    "rather than what the rows mean. Add a CODEOWNERS rule giving the owning product team " +
                    $"this file, or move it to {SchemaReader.SchemaRoot}/<Domain>/Tables/.");
            }
        }
    }

    private static void CheckAllowlistGrowth(
        CheckArgs args,
        string repoRoot,
        IEnumerable<string> changed,
        IReadOnlySet<string> allowlist,
        List<string> violations)
    {
        if (!changed.Contains(GrandfatheredTables.RepoRelativePath, StringComparer.Ordinal))
        {
            return;
        }

        var baseline = ReadBaselineAllowlist(args, repoRoot);
        if (baseline is null)
        {
            Console.WriteLine(
                $"  {GrandfatheredTables.RepoRelativePath} changed, but no baseline copy of it exists, " +
                "which is expected only for the change that first adds the list, so growth could not " +
                "be checked.");
            return;
        }

        foreach (var added in GrandfatheredTables.FindAddedEntries(allowlist, baseline))
        {
            violations.Add(
                $"{added}: added to {GrandfatheredTables.RepoRelativePath}. That list may shrink and must " +
                $"never grow. Move the table to {SchemaReader.SchemaRoot}/<Domain>/Tables/, or give its " +
                "domain folder a CODEOWNERS rule naming the product team, instead of exempting it from " +
                "ownership.");
        }
    }

    /// <summary>
    /// The allowlist as it stands at the base of the change. CI supplies it as a file because in a
    /// pull-request checkout HEAD already contains the change, which would make growth invisible.
    /// Null when no baseline exists, which is the case for the change that first adds the list.
    /// </summary>
    private static IReadOnlySet<string>? ReadBaselineAllowlist(CheckArgs args, string repoRoot)
    {
        if (args.BaselineAllowlist is { Length: > 0 } path)
        {
            return File.Exists(path) ? GrandfatheredTables.FromFile(path) : null;
        }

        return RunGit(repoRoot, reportFailure: false, "show", $"HEAD:{GrandfatheredTables.RepoRelativePath}")
            is { } lines
            ? GrandfatheredTables.Parse(lines)
            : null;
    }

    private static void CheckStaleness(string repoRoot, List<string> violations)
    {
        var inventory = SchemaReader.Read(repoRoot);
        if (!inventory.Errors.IsEmpty)
        {
            var first = inventory.Errors[0];
            throw new InvalidDataException(string.Create(
                CultureInfo.InvariantCulture,
                $"{inventory.Errors.Length} T-SQL parse error(s) under {SchemaReader.SchemaRoot}, first at " +
                $"{first.SchemaFile}({first.Line},{first.Column}): {first.Message} " +
                $"The committed map cannot be compared until the schema parses."));
        }

        var codeowners = CodeownersResolver.FromFile(Path.Combine(repoRoot, ".github", "CODEOWNERS"));
        var domains = DomainResolver.FromRepo(repoRoot);
        var expected = _utf8NoBom.GetBytes(
            ReportWriter.Serialize(OwnershipReport.Build(inventory.Tables, codeowners, domains)));

        var relative = $"{ScanArgs.DefaultOutDirectory}/{OwnershipReport.FileName}";
        var committed = Path.Combine(repoRoot, ScanArgs.DefaultOutDirectory, OwnershipReport.FileName);

        if (!File.Exists(committed))
        {
            violations.Add($"{relative}: missing. Run '{RegenerateCommand}' and commit the result.");
            return;
        }

        if (!File.ReadAllBytes(committed).AsSpan().SequenceEqual(expected))
        {
            violations.Add(
                $"{relative}: does not match a fresh scan of the current schema and CODEOWNERS. " +
                $"Run '{RegenerateCommand}' and commit the result.");
        }
    }

    private static int ReportViolations(List<string> violations)
    {
        if (violations.Count == 0)
        {
            Console.WriteLine("check: clean.");
            return ExitCode.Clean;
        }

        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture, $"check: {violations.Count} policy violation(s)."));
        foreach (var violation in violations)
        {
            Console.WriteLine($"  {violation}");
        }

        return ExitCode.PolicyViolation;
    }

    /// <summary>
    /// The paths CI computed, or the working tree when --changed-files is omitted. Null when the list
    /// could not be read, in which case the reason is already on stderr.
    /// </summary>
    private static string[]? ReadChangedFiles(CheckArgs args, string repoRoot)
    {
        if (args.ChangedFiles is { Length: > 0 } path)
        {
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"Error: --changed-files '{path}' does not exist.");
                return null;
            }

            return NormalizePaths(File.ReadAllLines(path));
        }

        var tracked = RunGit(
            repoRoot, reportFailure: true, "diff", "--name-only", "--diff-filter=d", "HEAD");
        var untracked = RunGit(
            repoRoot, reportFailure: true, "ls-files", "--others", "--exclude-standard", "--full-name");

        return tracked is null || untracked is null ? null : NormalizePaths([.. tracked, .. untracked]);
    }

    private static string[] NormalizePaths(IEnumerable<string> lines) =>
        [.. lines
            .Select(line => line.Trim().Replace('\\', '/'))
            .Where(line => line.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(line => line, StringComparer.Ordinal)];

    /// <summary>
    /// Standard output lines, or null when git is unavailable or reported failure. Arguments go through
    /// ArgumentList rather than a command string, so no path can be read as shell syntax.
    /// </summary>
    private static string[]? RunGit(string repoRoot, bool reportFailure, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                Fail("git could not be started");
                return null;
            }

            // Both pipes must drain concurrently. Reading one to completion first lets the other's
            // buffer fill, which blocks git before it can close the stream being read, and the wait
            // never returns. git warns per path on some configurations, so stderr can exceed it.
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            var output = outputTask.GetAwaiter().GetResult();
            var error = errorTask.GetAwaiter().GetResult();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Fail(error.Trim());
                return null;
            }

            return output.Split('\n');
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Fail(exception.Message);
            return null;
        }

        void Fail(string reason)
        {
            if (reportFailure)
            {
                Console.Error.WriteLine(
                    $"Error: 'git {string.Join(' ', arguments)}' failed in '{repoRoot}': {reason}. " +
                    "Pass --changed-files to supply the paths instead.");
            }
        }
    }
}
