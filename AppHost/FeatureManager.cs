using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// IInteractionService is still gated behind Aspire's experimental diagnostic. The dashboard prompt
// APIs are the whole point of this file, so the suppression is scoped here rather than repo-wide.
#pragma warning disable ASPIREINTERACTION001

namespace Bit.AppHost;

/// <summary>
/// A feature flag constant declared in <c>src/Core/Constants.cs</c>.
/// </summary>
/// <param name="Team">The <c>/* Team */</c> banner comment the constant was declared under.</param>
/// <param name="Constant">The C# constant name, e.g. <c>PolicyDrawers</c>.</param>
/// <param name="Key">The flag key, e.g. <c>pm-34804-policy-drawers</c>.</param>
internal sealed record FeatureFlag(string Team, string Constant, string Key);

/// <summary>
/// Dashboard tooling for toggling feature flags without hand-editing <c>dev/secrets.json</c>.
/// </summary>
/// <remarks>
/// Aspire's inputs dialog fixes its input set when the prompt opens — <c>LoadCallback</c> can only
/// mutate <c>Value</c>, <c>Options</c> and <c>Disabled</c> on inputs that already exist, and there is
/// no multi-select input type. A single dialog therefore cannot render a checkbox list that grows and
/// shrinks as you type. The flow is split in two instead: a filter dialog that reports its match count
/// live via <c>DynamicLoading</c>, then a checkbox dialog over the narrowed set.
/// </remarks>
internal static partial class FeatureManager
{
    /// <summary>Upper bound on checkboxes in one dialog, enforced by the filter stage's validation.</summary>
    private const int MaxCheckboxes = 40;

    /// <summary>Sentinel <see cref="InputType.Choice"/> value meaning "do not filter by team".</summary>
    private const string AllTeams = "*";

    private const string UngroupedTeam = "Ungrouped";

    [GeneratedRegex(@"^\s*/\*\s*(?<team>.+?)\s*\*/\s*$")]
    private static partial Regex TeamBannerPattern { get; }

    [GeneratedRegex(@"^\s*public const string\s+(?<constant>\w+)\s*=\s*""(?<key>[^""]+)""\s*;")]
    private static partial Regex FlagConstantPattern { get; }

    [GeneratedRegex(@"^\s*""(?<key>[^""]+)""\s*:\s*(?<value>true|false|""true""|""false"")\s*,?\s*$")]
    private static partial Regex FlagValueEntryPattern { get; }

    /// <summary>
    /// Adds the highlighted "Feature Manager" command to the secrets setup resource. The command
    /// rewrites <c>dev/secrets.json</c>, re-runs the resource so the new values reach every project's
    /// user secrets, then restarts each project so they are read at startup.
    /// </summary>
    public static IResourceBuilder<ExecutableResource> WithFeatureManager(
        this IResourceBuilder<ExecutableResource> secretsSetup,
        IDistributedApplicationBuilder builder)
    {
        // Anchored on the AppHost project rather than the WorkingDirectory setting, which points at
        // dev/ (where the migration and secrets scripts live) rather than the repository root.
        var repositoryRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, ".."));

        // Same script the setup-secrets resource runs, invoked directly rather than by re-running that
        // resource. See ReapplyAsync.
        var secretsScript = builder.Configuration["Scripts:SecretsSetup"]
            ?? throw new InvalidOperationException("Missing required configuration: Scripts:SecretsSetup");

        return secretsSetup.WithCommand(
            name: "feature-manager",
            displayName: "Feature Manager",
            executeCommand: context => ExecuteAsync(context, repositoryRoot, secretsScript),
            commandOptions: new CommandOptions
            {
                Description =
                    "Filter, then tick a checkbox per flag. Rewrites dev/secrets.json, reapplies it to "
                    + "every project, and restarts the services.",
                IconName = "Flag",
                IconVariant = IconVariant.Filled,
                IsHighlighted = true,
                // setup-secrets sits in Finished once its script exits; without this the command would
                // be greyed out for all but the few seconds the script is actually running.
                UpdateState = _ => ResourceCommandState.Enabled
            });
    }

    private static async Task<ExecuteCommandResult> ExecuteAsync(
        ExecuteCommandContext context,
        string repositoryRoot,
        string secretsScript)
    {
        var cancellationToken = context.CancellationToken;
        var interaction = context.ServiceProvider.GetRequiredService<IInteractionService>();

        if (!interaction.IsAvailable)
        {
            return CommandResults.Failure("Feature Manager needs the Aspire dashboard to prompt for input.");
        }

        var constantsPath = Path.Combine(repositoryRoot, "src", "Core", "Constants.cs");
        var secretsPath = Path.Combine(repositoryRoot, "dev", "secrets.json");

        if (!File.Exists(constantsPath))
        {
            return CommandResults.Failure($"Could not find {constantsPath}.");
        }

        if (!File.Exists(secretsPath))
        {
            return CommandResults.Failure($"Could not find {secretsPath}. Complete the server setup guide first.");
        }

        var catalog = ParseCatalog(await File.ReadAllTextAsync(constantsPath, cancellationToken));
        if (catalog.Count == 0)
        {
            return CommandResults.Failure("No feature flags found in Constants.cs. Has FeatureFlagKeys moved?");
        }

        var secretsText = await File.ReadAllTextAsync(secretsPath, cancellationToken);
        if (!TryLocateFlagValuesBody(secretsText, out var bodyStart, out var bodyLength))
        {
            return CommandResults.Failure(
                "Could not find globalSettings.launchDarkly.flagValues in dev/secrets.json. " +
                "Add an empty \"flagValues\": { } block and try again.");
        }

        var body = secretsText.Substring(bodyStart, bodyLength);
        var preservedComments = ExtractCommentLines(body);

        var enabled = await PromptForFlagSelectionAsync(
            interaction, catalog, ParseEnabledKeys(body), cancellationToken);

        if (enabled is null)
        {
            return CommandResults.Canceled();
        }

        var rewritten = string.Concat(
            secretsText.AsSpan(0, bodyStart),
            RenderFlagValuesBody(enabled, preservedComments, DetectIndent(secretsText, bodyStart)),
            secretsText.AsSpan(bodyStart + bodyLength));

        await File.WriteAllTextAsync(secretsPath, rewritten, cancellationToken);
        context.Logger.LogInformation("Wrote {Count} enabled feature flag(s) to {SecretsPath}.", enabled.Count, secretsPath);

        return await ReapplyAndRestartAsync(context, repositoryRoot, secretsScript, enabled.Count);
    }

    /// <summary>
    /// Filter dialog, then a checkbox dialog over the narrowed set. Two dialogs because the input set
    /// is fixed once a prompt opens — <c>LoadCallback</c> can only mutate <c>Value</c>, <c>Options</c>
    /// and <c>Disabled</c> on inputs that already exist, so a checkbox list cannot grow or shrink.
    /// </summary>
    private static async Task<HashSet<string>?> PromptForFlagSelectionAsync(
        IInteractionService interaction,
        IReadOnlyList<FeatureFlag> catalog,
        HashSet<string> enabled,
        CancellationToken cancellationToken)
    {
        var teamOptions = BuildTeamOptions(catalog);
        var filter = string.Empty;
        var team = AllTeams;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var narrowed = await PromptForFilterAsync(interaction, catalog, teamOptions, team, filter, cancellationToken);
            if (narrowed is null)
            {
                return null;
            }

            (team, filter) = narrowed.Value;

            var selection = await PromptForFlagsAsync(
                interaction, Match(catalog, team, filter), enabled, cancellationToken);

            if (selection is null)
            {
                // Dismissing the checkbox dialog drops back to the filter rather than aborting, so a
                // mis-typed filter costs one click instead of restarting the command.
                continue;
            }

            foreach (var (key, isEnabled) in selection)
            {
                if (isEnabled)
                {
                    enabled.Add(key);
                }
                else
                {
                    enabled.Remove(key);
                }
            }

            return enabled;
        }
    }

    /// <summary>
    /// Stage one: pick a team and type a filter, with a live match count driven by
    /// <see cref="InputLoadOptions.DependsOnInputs"/>. Returns <see langword="null"/> if dismissed.
    /// </summary>
    private static async Task<(string Team, string Filter)?> PromptForFilterAsync(
        IInteractionService interaction,
        IReadOnlyList<FeatureFlag> catalog,
        IReadOnlyList<KeyValuePair<string, string>> teamOptions,
        string team,
        string filter,
        CancellationToken cancellationToken)
    {
        var result = await interaction.PromptInputsAsync(
            "Feature Manager",
            $"{catalog.Count} flags declared in `src/Core/Constants.cs`. " +
            $"Narrow the list to {MaxCheckboxes} or fewer to pick them.",
            [
                new InteractionInput
                {
                    Name = "team",
                    Label = "Team",
                    InputType = InputType.Choice,
                    Options = teamOptions,
                    Value = team
                },
                new InteractionInput
                {
                    Name = "filter",
                    Label = "Filter",
                    InputType = InputType.Text,
                    Placeholder = "pm-34, sdk, cipher…",
                    Description = "Matches against the flag key and the C# constant name.",
                    Value = filter
                },
                new InteractionInput
                {
                    Name = "matches",
                    Label = "Matches",
                    InputType = InputType.Text,
                    Disabled = true,
                    DynamicLoading = new InputLoadOptions
                    {
                        DependsOnInputs = ["team", "filter"],
                        AlwaysLoadOnStart = true,
                        LoadCallback = loadContext =>
                        {
                            var count = Match(
                                catalog,
                                loadContext.AllInputs["team"].Value,
                                loadContext.AllInputs["filter"].Value).Count;

                            loadContext.Input.Value = count switch
                            {
                                0 => "No flags match",
                                1 => "1 flag",
                                _ when count > MaxCheckboxes => $"{count} flags — too many to show",
                                _ => $"{count} flags"
                            };

                            return Task.CompletedTask;
                        }
                    }
                }
            ],
            new InputsDialogInteractionOptions
            {
                PrimaryButtonText = "Next",
                EnableMessageMarkdown = true,
                ValidationCallback = validationContext =>
                {
                    var matches = Match(
                        catalog,
                        validationContext.Inputs["team"].Value,
                        validationContext.Inputs["filter"].Value);

                    if (matches.Count == 0)
                    {
                        validationContext.AddValidationError(validationContext.Inputs["filter"], "No flags match.");
                    }
                    else if (matches.Count > MaxCheckboxes)
                    {
                        validationContext.AddValidationError(
                            validationContext.Inputs["filter"],
                            $"{matches.Count} flags match. Narrow to {MaxCheckboxes} or fewer.");
                    }

                    return Task.CompletedTask;
                }
            },
            cancellationToken);

        return result.Canceled
            ? null
            : (result.Data["team"].Value ?? AllTeams, result.Data["filter"].Value ?? string.Empty);
    }

    /// <summary>
    /// Stage two: one <see cref="InputType.Boolean"/> checkbox per matched flag, pre-checked from the
    /// current <c>dev/secrets.json</c> state. Returns <see langword="null"/> if dismissed.
    /// </summary>
    private static async Task<IReadOnlyList<(string Key, bool Enabled)>?> PromptForFlagsAsync(
        IInteractionService interaction,
        IReadOnlyList<FeatureFlag> matches,
        IReadOnlySet<string> enabled,
        CancellationToken cancellationToken)
    {
        var inputs = matches
            .Select(flag => new InteractionInput
            {
                Name = flag.Key,
                Label = flag.Constant,
                Description = $"`{flag.Key}` · {flag.Team}",
                EnableDescriptionMarkdown = true,
                InputType = InputType.Boolean,
                Value = enabled.Contains(flag.Key) ? "true" : "false"
            })
            .ToList();

        var result = await interaction.PromptInputsAsync(
            "Feature Manager",
            $"{matches.Count} flag(s). Applying rewrites `dev/secrets.json`, re-runs `setup-secrets`, " +
            "and restarts every service.",
            inputs,
            new InputsDialogInteractionOptions
            {
                PrimaryButtonText = "Apply and restart",
                EnableMessageMarkdown = true
            },
            cancellationToken);

        return result.Canceled
            ? null
            : [.. result.Data.Select(input => (input.Name, IsTrue(input.Value)))];
    }

    /// <summary>
    /// Re-runs the secrets script so the rewritten values land in every project's user secrets, then
    /// restarts each project resource — configuration is only read at startup.
    /// </summary>
    /// <remarks>
    /// The secrets script is invoked directly rather than by re-running the <c>setup-secrets</c>
    /// resource. This command is hosted on that resource, and re-running a resource tears down the
    /// command handlers attached to it — the handler simply stops, with no exception and no completion,
    /// so the service restarts below never happen. That applies to <c>start</c> as much as
    /// <c>restart</c>; a resource sitting in a terminal state still has to be torn down to run again.
    /// </remarks>
    private static async Task<ExecuteCommandResult> ReapplyAndRestartAsync(
        ExecuteCommandContext context,
        string repositoryRoot,
        string secretsScript,
        int enabledCount)
    {
        var cancellationToken = context.CancellationToken;
        var commands = context.ServiceProvider.GetRequiredService<ResourceCommandService>();
        var model = context.ServiceProvider.GetRequiredService<DistributedApplicationModel>();

        var exitCode = await ReapplySecretsAsync(context, repositoryRoot, secretsScript, cancellationToken);
        if (exitCode != 0)
        {
            return CommandResults.Failure(
                $"Flags were written, but {secretsScript} exited with code {exitCode}. "
                + "Services were left running with the previous values.");
        }

        var projects = model.Resources.OfType<ProjectResource>().ToList();
        var failed = new List<string>();

        foreach (var project in projects)
        {
            var result = await commands.ExecuteCommandAsync(
                project, KnownResourceCommands.RestartCommand, cancellationToken);

            if (!result.Success)
            {
                failed.Add(project.Name);
                context.Logger.LogWarning(
                    "Failed to restart {Project}: {Error}", project.Name, result.ErrorMessage);
            }
        }

        if (failed.Count > 0)
        {
            return CommandResults.Failure(
                $"{enabledCount} flag(s) applied, but these services did not restart: {string.Join(", ", failed)}.");
        }

        context.Logger.LogInformation(
            "Applied {Count} feature flag(s) and restarted {Restarted} service(s).", enabledCount, projects.Count);

        return CommandResults.Success();
    }

    /// <summary>
    /// Runs <c>dev/setup_secrets.ps1</c> as a child process, mirroring the <c>setup-secrets</c> resource
    /// definition, and relays its output to the resource log so the dashboard still shows what happened.
    /// </summary>
    private static async Task<int> ReapplySecretsAsync(
        ExecuteCommandContext context,
        string repositoryRoot,
        string secretsScript,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = Path.Combine(repositoryRoot, "dev"),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(secretsScript);
        startInfo.ArgumentList.Add("-clear");

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                context.Logger.LogInformation("{Line}", e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                context.Logger.LogWarning("{Line}", e.Data);
            }
        };

        context.Logger.LogInformation("Reapplying secrets: pwsh -File {Script} -clear", secretsScript);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }

    /// <summary>
    /// Parses the <c>FeatureFlagKeys</c> class out of <c>Constants.cs</c>, attributing each constant to
    /// the most recent <c>/* Team */</c> banner. <c>FeatureFlagKeys.GetAllKeys()</c> would be more
    /// robust, but the AppHost cannot reference Core's assembly and that method discards the grouping —
    /// which is the most useful filter axis across this many flags.
    /// </summary>
    private static List<FeatureFlag> ParseCatalog(string source)
    {
        var flags = new List<FeatureFlag>();
        var classStart = source.IndexOf("public static class FeatureFlagKeys", StringComparison.Ordinal);
        if (classStart < 0)
        {
            return flags;
        }

        var team = UngroupedTeam;

        foreach (var line in source[classStart..].Split('\n'))
        {
            // The constants all precede the class's helper methods; stop at the first one.
            if (line.Contains("static List<string> GetAllKeys", StringComparison.Ordinal))
            {
                break;
            }

            var banner = TeamBannerPattern.Match(line);
            if (banner.Success)
            {
                team = banner.Groups["team"].Value;
                continue;
            }

            var constant = FlagConstantPattern.Match(line);
            if (constant.Success)
            {
                flags.Add(new FeatureFlag(team, constant.Groups["constant"].Value, constant.Groups["key"].Value));
            }
        }

        return flags;
    }

    private static List<FeatureFlag> Match(IReadOnlyList<FeatureFlag> catalog, string? team, string? filter) =>
        [.. catalog.Where(flag =>
            (string.IsNullOrEmpty(team) || team == AllTeams || flag.Team == team)
            && (string.IsNullOrWhiteSpace(filter)
                || flag.Key.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || flag.Constant.Contains(filter, StringComparison.OrdinalIgnoreCase)))];

    private static List<KeyValuePair<string, string>> BuildTeamOptions(IReadOnlyList<FeatureFlag> catalog) =>
        [
            new(AllTeams, $"All teams ({catalog.Count})"),
            .. catalog
                .GroupBy(flag => flag.Team)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new KeyValuePair<string, string>(group.Key, $"{group.Key} ({group.Count()})"))
        ];

    /// <summary>
    /// Locates the body of the <c>flagValues</c> object — the span between its braces, exclusive.
    /// Only that span is rewritten, so the rest of <c>dev/secrets.json</c>, comments included, survives.
    /// </summary>
    private static bool TryLocateFlagValuesBody(string secretsText, out int bodyStart, out int bodyLength)
    {
        bodyStart = 0;
        bodyLength = 0;

        var marker = secretsText.IndexOf("\"flagValues\"", StringComparison.Ordinal);
        if (marker < 0)
        {
            return false;
        }

        var open = secretsText.IndexOf('{', marker);
        if (open < 0)
        {
            return false;
        }

        // Flag keys never contain braces, so plain counting is sufficient here.
        var depth = 0;
        for (var i = open; i < secretsText.Length; i++)
        {
            if (secretsText[i] == '{')
            {
                depth++;
            }
            else if (secretsText[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    bodyStart = open + 1;
                    bodyLength = i - bodyStart;
                    return true;
                }
            }
        }

        return false;
    }

    private static HashSet<string> ParseEnabledKeys(string body)
    {
        var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in body.Split('\n'))
        {
            var entry = FlagValueEntryPattern.Match(line);
            if (entry.Success && IsTrue(entry.Groups["value"].Value.Trim('"')))
            {
                enabled.Add(entry.Groups["key"].Value);
            }
        }

        return enabled;
    }

    /// <summary>
    /// Keeps commented-out entries in the block. Developers park flags there deliberately, and losing
    /// them on every apply would be a nasty surprise.
    /// </summary>
    private static List<string> ExtractCommentLines(string body) =>
        [.. body.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("//", StringComparison.Ordinal))];

    /// <summary>Infers the indentation of entries inside the block from the line the block opens on.</summary>
    private static string DetectIndent(string secretsText, int bodyStart)
    {
        // Start from the opening brace itself: bodyStart is the character after it, which is usually
        // the newline that ends the line and would otherwise measure the first entry instead.
        var lineStart = secretsText.LastIndexOf('\n', Math.Clamp(bodyStart - 1, 0, secretsText.Length - 1));
        var openingIndent = 0;

        for (var i = lineStart + 1; i < secretsText.Length && secretsText[i] is ' '; i++)
        {
            openingIndent++;
        }

        return new string(' ', openingIndent + 2);
    }

    private static string RenderFlagValuesBody(
        IReadOnlySet<string> enabled,
        IReadOnlyList<string> preservedComments,
        string indent)
    {
        var closingIndent = indent.Length >= 2 ? indent[..^2] : string.Empty;

        if (enabled.Count == 0 && preservedComments.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder("\n");

        foreach (var comment in preservedComments)
        {
            builder.Append(indent).Append(comment).Append('\n');
        }

        var ordered = enabled.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            builder
                .Append(indent)
                .Append('"').Append(ordered[i]).Append("\": true")
                .Append(i < ordered.Count - 1 ? "," : string.Empty)
                .Append('\n');
        }

        return builder.Append(closingIndent).ToString();
    }

    private static bool IsTrue(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
