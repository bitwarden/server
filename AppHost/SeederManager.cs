using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// See FeatureManager.cs — IInteractionService is still behind Aspire's experimental diagnostic.
#pragma warning disable ASPIREINTERACTION001

namespace Bit.AppHost;

/// <summary>
/// Holds the arguments chosen in the dashboard until the seeder resource next starts. The resource's
/// <c>WithArgs</c> callback runs on every start, which is the only hook that can vary an executable's
/// command line after the application model is built.
/// </summary>
internal sealed class PendingSeed
{
    private volatile IReadOnlyList<string> _args = [];

    public IReadOnlyList<string> Args
    {
        get => _args;
        set => _args = value;
    }
}

/// <summary>
/// Wires <c>util/SeederUtility</c> into the dashboard as an on-demand resource with a "Seed" command
/// that collects options through prompts rather than requiring a hand-written command line.
/// </summary>
internal static class SeederManager
{
    private const string ResourceName = "seeder";

    private static readonly string[] s_structures =
        ["Traditional", "Spotify", "Modern", "Government", "SchoolDistrict", "Healthcare", "Startup"];

    private static readonly string[] s_regions =
        ["NorthAmerica", "Europe", "AsiaPacific", "LatinAmerica", "MiddleEast", "Africa", "Global"];

    private static readonly string[] s_densities =
        ["balanced", "highPerm", "highCollection", "broad", "minimal", "groupHeavy", "sparse"];

    private static readonly string[] s_planTypes =
    [
        "enterprise-annually", "enterprise-monthly", "teams-annually", "teams-monthly",
        "teams-starter", "families-annually", "free"
    ];

    /// <summary>
    /// Adds the seeder as an explicit-start executable carrying a highlighted "Seed" command. Nothing
    /// runs until the command is used — seeding is destructive enough that it should never fire as a
    /// side effect of starting the app host.
    /// </summary>
    public static IResourceBuilder<ExecutableResource> ConfigureSeeder(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<SqlServerDatabaseResource> db,
        IResourceBuilder<ExecutableResource> secretsSetup,
        IResourceBuilder<AzureStorageResource> azurite)
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, ".."));
        var pending = new PendingSeed();

        // dev/secrets.json configures no attachment storage, so GlobalSettings.Attachment falls through
        // to NoopAttachmentStorageService and any preset carrying attachment fixtures fails outright.
        // Binding the emulator's blob connection string here points the seeder at Azurite without
        // editing the shared secrets file — environment variables are added after user secrets in
        // GlobalSettingsFactory, so this wins.
        var blobs = azurite.AddBlobs("blobs");

        return builder
            .AddExecutable(
                ResourceName,
                "dotnet",
                repositoryRoot,
                "run", "--project", Path.Combine("util", "SeederUtility"), "--no-launch-profile", "--")
            .WithExplicitStart()
            .WaitFor(db)
            .WaitFor(azurite)
            .WaitForCompletion(secretsSetup)
            .WithEnvironment("globalSettings__attachment__connectionString", blobs)
            .ExcludeFromManifest()
            .WithArgs(context =>
            {
                foreach (var argument in pending.Args)
                {
                    context.Args.Add(argument);
                }
            })
            .WithCommand(
                name: "seed",
                displayName: "Seed",
                executeCommand: context => ExecuteAsync(context, repositoryRoot, pending),
                commandOptions: new CommandOptions
                {
                    Description = "Choose what to seed, then run util/SeederUtility against the dev database.",
                    IconName = "Sprout",
                    IconVariant = IconVariant.Filled,
                    IsHighlighted = true,
                    UpdateState = _ => ResourceCommandState.Enabled
                });
    }

    private static async Task<ExecuteCommandResult> ExecuteAsync(
        ExecuteCommandContext context,
        string repositoryRoot,
        PendingSeed pending)
    {
        var cancellationToken = context.CancellationToken;
        var interaction = context.ServiceProvider.GetRequiredService<IInteractionService>();

        if (!interaction.IsAvailable)
        {
            return CommandResults.Failure("Seeding needs the Aspire dashboard to prompt for options.");
        }

        var kind = await PromptForKindAsync(interaction, cancellationToken);
        if (kind is null)
        {
            return CommandResults.Canceled();
        }

        var arguments = kind switch
        {
            "preset" => await PromptForPresetAsync(interaction, repositoryRoot, cancellationToken),
            "organization" => await PromptForOrganizationAsync(interaction, cancellationToken),
            _ => await PromptForIndividualAsync(interaction, cancellationToken)
        };

        if (arguments is null)
        {
            return CommandResults.Canceled();
        }

        pending.Args = arguments;
        context.Logger.LogInformation("Seeding with: {Arguments}", string.Join(' ', arguments));

        return await RunSeederAsync(context);
    }

    private static async Task<string?> PromptForKindAsync(
        IInteractionService interaction,
        CancellationToken cancellationToken)
    {
        var result = await interaction.PromptInputsAsync(
            "Seed",
            "Pick what to create. The next dialog collects options for that command.",
            [
                new InteractionInput
                {
                    Name = "kind",
                    Label = "What to seed",
                    InputType = InputType.Choice,
                    Value = "preset",
                    Options =
                    [
                        new("preset", "Preset — a curated, reproducible fixture"),
                        new("organization", "Organization — generated, full control over shape"),
                        new("individual", "Individual — a single user account")
                    ]
                }
            ],
            new InputsDialogInteractionOptions { PrimaryButtonText = "Next" },
            cancellationToken);

        return result.Canceled ? null : result.Data["kind"].Value;
    }

    private static async Task<List<string>?> PromptForPresetAsync(
        IInteractionService interaction,
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        var presets = DiscoverPresets(repositoryRoot);
        if (presets.Count == 0)
        {
            return null;
        }

        var result = await interaction.PromptInputsAsync(
            "Seed a preset",
            $"{presets.Count} presets found in `util/Seeder/Seeds/fixtures/presets`.",
            [
                new InteractionInput
                {
                    Name = "name",
                    Label = "Preset",
                    InputType = InputType.Choice,
                    Required = true,
                    Options = presets,
                    Value = presets[0].Key
                },
                new InteractionInput
                {
                    Name = "mangle",
                    Label = "Mangle IDs and emails",
                    Description = "Makes every run unique so the same preset can be seeded repeatedly.",
                    InputType = InputType.Boolean,
                    Value = "true"
                },
                Optional("orgName", "Organization name", "Overrides the preset's display name"),
                Optional("ownerEmail", "Owner email", "Defaults to owner@<preset domain>"),
                Optional("password", "Password", "Defaults to asdfasdfasdf"),
                OptionalNumber("kdfIterations", "KDF iterations", "Blank uses the preset value. Minimum 5000.")
            ],
            new InputsDialogInteractionOptions
            {
                PrimaryButtonText = "Seed",
                EnableMessageMarkdown = true,
                ValidationCallback = validation =>
                {
                    ValidateEmail(validation, "ownerEmail");
                    ValidateKdf(validation, "kdfIterations");
                    return Task.CompletedTask;
                }
            },
            cancellationToken);

        if (result.Canceled)
        {
            return null;
        }

        var arguments = new List<string> { "preset", "--name", result.Data["name"].Value! };
        AddFlag(arguments, result.Data, "mangle", "--mangle");
        AddValue(arguments, result.Data, "orgName", "--org-name");
        AddValue(arguments, result.Data, "ownerEmail", "--owner-email");
        AddValue(arguments, result.Data, "password", "--password");
        AddValue(arguments, result.Data, "kdfIterations", "--kdf-iterations");
        return arguments;
    }

    private static async Task<List<string>?> PromptForOrganizationAsync(
        IInteractionService interaction,
        CancellationToken cancellationToken)
    {
        var result = await interaction.PromptInputsAsync(
            "Seed an organization",
            "Generates an org to the shape you describe. Only name, domain and users are required.",
            [
                new InteractionInput
                {
                    Name = "name", Label = "Organization name", InputType = InputType.Text, Required = true,
                    Placeholder = "Acme"
                },
                new InteractionInput
                {
                    Name = "domain", Label = "Email domain", InputType = InputType.Text, Required = true,
                    Placeholder = "acme.example",
                    Description = "Must end in .example (RFC 2606)."
                },
                new InteractionInput
                {
                    Name = "users", Label = "Users", InputType = InputType.Number, Required = true, Value = "10"
                },
                new InteractionInput { Name = "ciphers", Label = "Ciphers", InputType = InputType.Number, Value = "0" },
                new InteractionInput { Name = "groups", Label = "Groups", InputType = InputType.Number, Value = "0" },
                new InteractionInput
                {
                    Name = "collections", Label = "Collections", InputType = InputType.Number, Value = "0"
                },
                ChoiceOf("structure", "Structure", s_structures, "(default)"),
                ChoiceOf("region", "Region", s_regions, "(default)"),
                ChoiceOf("density", "Density profile", s_densities, "(none)"),
                ChoiceOf("planType", "Plan type", s_planTypes, null),
                new InteractionInput
                {
                    Name = "mangle", Label = "Mangle IDs and emails", InputType = InputType.Boolean, Value = "false"
                },
                Optional("password", "Password", "Defaults to asdfasdfasdf")
            ],
            new InputsDialogInteractionOptions
            {
                PrimaryButtonText = "Seed",
                ValidationCallback = validation =>
                {
                    var domain = validation.Inputs["domain"].Value;
                    if (string.IsNullOrWhiteSpace(domain) || !domain.EndsWith(".example", StringComparison.OrdinalIgnoreCase))
                    {
                        validation.AddValidationError(validation.Inputs["domain"], "Domain must end in .example");
                    }

                    if (!int.TryParse(validation.Inputs["users"].Value, out var users) || users < 1)
                    {
                        validation.AddValidationError(validation.Inputs["users"], "Users must be at least 1.");
                    }

                    return Task.CompletedTask;
                }
            },
            cancellationToken);

        if (result.Canceled)
        {
            return null;
        }

        var arguments = new List<string>
        {
            "organization",
            "--name", result.Data["name"].Value!,
            "--domain", result.Data["domain"].Value!,
            "--users", result.Data["users"].Value!
        };

        AddValue(arguments, result.Data, "ciphers", "--ciphers");
        AddValue(arguments, result.Data, "groups", "--groups");
        AddValue(arguments, result.Data, "collections", "--collections");
        AddValue(arguments, result.Data, "structure", "--org-structure");
        AddValue(arguments, result.Data, "region", "--region");
        AddValue(arguments, result.Data, "density", "--density");
        AddValue(arguments, result.Data, "planType", "--plan-type");
        AddFlag(arguments, result.Data, "mangle", "--mangle");
        AddValue(arguments, result.Data, "password", "--password");
        return arguments;
    }

    private static async Task<List<string>?> PromptForIndividualAsync(
        IInteractionService interaction,
        CancellationToken cancellationToken)
    {
        var result = await interaction.PromptInputsAsync(
            "Seed an individual user",
            "Leave both names blank for a random identity — mangling is then applied automatically.",
            [
                new InteractionInput
                {
                    Name = "subscription", Label = "Subscription", InputType = InputType.Choice, Required = true,
                    Value = "free",
                    Options = [new("free", "Free"), new("premium", "Premium")]
                },
                Optional("firstName", "First name", "Provide both names or neither"),
                Optional("lastName", "Last name", "Provide both names or neither"),
                Optional("email", "Email", "Defaults to {first}.{last}@individual.example"),
                new InteractionInput
                {
                    Name = "vault", Label = "Generate personal vault",
                    Description = "Creates roughly 75 ciphers and some folders.",
                    InputType = InputType.Boolean, Value = "false"
                },
                new InteractionInput
                {
                    Name = "selfHosted", Label = "Self-hosted", InputType = InputType.Boolean, Value = "false",
                    Description = "Writes a licence file so premium is recognised on self-hosted instances."
                },
                Optional("password", "Password", "Defaults to asdfasdfasdf")
            ],
            new InputsDialogInteractionOptions
            {
                PrimaryButtonText = "Seed",
                ValidationCallback = validation =>
                {
                    var hasFirst = !string.IsNullOrWhiteSpace(validation.Inputs["firstName"].Value);
                    var hasLast = !string.IsNullOrWhiteSpace(validation.Inputs["lastName"].Value);

                    if (hasFirst != hasLast)
                    {
                        validation.AddValidationError(
                            validation.Inputs[hasFirst ? "lastName" : "firstName"],
                            "Provide both names, or neither.");
                    }

                    ValidateEmail(validation, "email");
                    return Task.CompletedTask;
                }
            },
            cancellationToken);

        if (result.Canceled)
        {
            return null;
        }

        var arguments = new List<string> { "individual", "--subscription", result.Data["subscription"].Value! };
        AddValue(arguments, result.Data, "firstName", "--first-name");
        AddValue(arguments, result.Data, "lastName", "--last-name");
        AddValue(arguments, result.Data, "email", "--email");
        AddFlag(arguments, result.Data, "vault", "--vault");
        AddFlag(arguments, result.Data, "selfHosted", "--self-hosted");
        AddValue(arguments, result.Data, "password", "--password");
        return arguments;
    }

    /// <summary>
    /// Starts the seeder and waits for that run to exit. Delegates to <see cref="ResourceRunner"/>,
    /// which distinguishes the new run from the previous one — a plain wait for a terminal state would
    /// match the last run's and report success before this one had started.
    /// </summary>
    private static async Task<ExecuteCommandResult> RunSeederAsync(ExecuteCommandContext context)
    {
        string finalState;
        try
        {
            finalState = await ResourceRunner.RunToCompletionAsync(
                context.ServiceProvider, ResourceName, context.Logger, context.CancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return CommandResults.Failure(exception.Message);
        }

        return finalState == KnownResourceStates.Finished
            ? CommandResults.Success()
            : CommandResults.Failure($"Seeder ended in state '{finalState}'. Check its logs for details.");
    }

    /// <summary>
    /// Reads the preset catalog off disk. <c>preset --list</c> would be authoritative, but it means
    /// building and running the seeder before the dialog can open; the file layout maps one-to-one onto
    /// the preset names (<c>qa/enterprise-basic.json</c> → <c>qa.enterprise-basic</c>).
    /// </summary>
    private static List<KeyValuePair<string, string>> DiscoverPresets(string repositoryRoot)
    {
        var root = Path.Combine(repositoryRoot, "util", "Seeder", "Seeds", "fixtures", "presets");
        if (!Directory.Exists(root))
        {
            return [];
        }

        return
        [
            .. Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
                .Select(path => new
                {
                    Category = Path.GetFileName(Path.GetDirectoryName(path)) ?? string.Empty,
                    Name = Path.GetFileNameWithoutExtension(path)
                })
                .Where(preset => !string.IsNullOrEmpty(preset.Category))
                .OrderBy(preset => preset.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase)
                .Select(preset => new KeyValuePair<string, string>(
                    $"{preset.Category}.{preset.Name}",
                    $"{preset.Category} · {preset.Name}"))
        ];
    }

    private static InteractionInput Optional(string name, string label, string description) => new()
    {
        Name = name,
        Label = label,
        Description = description,
        InputType = InputType.Text,
        Value = string.Empty
    };

    private static InteractionInput OptionalNumber(string name, string label, string description) => new()
    {
        Name = name,
        Label = label,
        Description = description,
        InputType = InputType.Number,
        Value = string.Empty
    };

    /// <summary>
    /// A Choice over a fixed set of CLI-accepted values. When <paramref name="unsetLabel"/> is supplied
    /// an empty first option is added, and selecting it omits the flag so the CLI default applies.
    /// </summary>
    private static InteractionInput ChoiceOf(string name, string label, string[] values, string? unsetLabel)
    {
        List<KeyValuePair<string, string>> options = unsetLabel is null ? [] : [new(string.Empty, unsetLabel)];
        options.AddRange(values.Select(value => new KeyValuePair<string, string>(value, value)));

        return new InteractionInput
        {
            Name = name,
            Label = label,
            InputType = InputType.Choice,
            Options = options,
            Value = options[0].Key
        };
    }

    private static void AddValue(List<string> arguments, InteractionInputCollection inputs, string name, string flag)
    {
        var value = inputs[name].Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        arguments.Add(flag);
        arguments.Add(value.Trim());
    }

    private static void AddFlag(List<string> arguments, InteractionInputCollection inputs, string name, string flag)
    {
        if (string.Equals(inputs[name].Value, "true", StringComparison.OrdinalIgnoreCase))
        {
            arguments.Add(flag);
        }
    }

    private static void ValidateEmail(InputsDialogValidationContext validation, string name)
    {
        var value = validation.Inputs[name].Value;
        if (!string.IsNullOrWhiteSpace(value) && !value.Contains('@'))
        {
            validation.AddValidationError(validation.Inputs[name], "Must be a valid email address.");
        }
    }

    private static void ValidateKdf(InputsDialogValidationContext validation, string name)
    {
        var value = validation.Inputs[name].Value;
        if (!string.IsNullOrWhiteSpace(value) && (!int.TryParse(value, out var iterations) || iterations < 5_000))
        {
            validation.AddValidationError(validation.Inputs[name], "KDF iterations must be at least 5000.");
        }
    }
}
