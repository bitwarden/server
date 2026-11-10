namespace Bit.AppHost.Configuration;

/// <summary>
/// The AppHost settings bound from configuration. Property names are the configuration keys; a <c>required</c>
/// property whose key is absent stops the AppHost before any resource starts.
/// </summary>
internal sealed record AppHostSettings
{
    /// <summary>
    /// Runs every service in self-hosted mode, on its base port plus one.
    /// </summary>
    internal bool SelfHost { get; init; }

    /// <summary>
    /// Path to the <c>apps</c> folder of a clients repository checkout, relative to the AppHost directory.
    /// </summary>
    internal required string ClientsPath { get; init; }

    /// <summary>
    /// Directory the setup and migration scripts run from, relative to the AppHost directory.
    /// </summary>
    internal required string WorkingDirectory { get; init; }

    /// <summary>
    /// Per-service settings; every <see cref="MonolithService"/> needs an entry.
    /// </summary>
    internal required Dictionary<MonolithService, ServiceSettings> Services { get; init; }

    /// <summary>
    /// Extra projects to run alongside the Bitwarden services, keyed by resource name.
    /// </summary>
    internal Dictionary<string, AdditionalProjectSettings> AdditionalProjects { get; init; } = [];

    /// <summary>
    /// The SQL Server container.
    /// </summary>
    internal required DatabaseSettings Database { get; init; }

    /// <summary>
    /// File names of the PowerShell scripts in <see cref="WorkingDirectory"/>.
    /// </summary>
    internal required ScriptSettings Scripts { get; init; }

    /// <summary>
    /// Auth token for the ngrok tunnel; the tunnel is skipped when empty.
    /// </summary>
    internal string? NgrokAuthToken { get; init; }

    /// <summary>
    /// The MailCatcher container.
    /// </summary>
    internal required MailCatcherSettings MailCatcher { get; init; }

    /// <summary>
    /// The Redis container.
    /// </summary>
    internal required ContainerSettings Redis { get; init; }

    /// <summary>
    /// The SimpleSAMLphp identity provider container.
    /// </summary>
    internal required ContainerSettings Idp { get; init; }

    /// <summary>
    /// The web vault npm watch build.
    /// </summary>
    internal required WebFrontendSettings WebFrontend { get; init; }
}

/// <summary>
/// Settings for one Bitwarden service.
/// </summary>
internal sealed record ServiceSettings
{
    /// <summary>
    /// Host port of the service's <c>http</c> endpoint in cloud mode; self-hosted mode adds one.
    /// </summary>
    internal required int BasePort { get; init; }
}

/// <summary>
/// An extra project added through configuration instead of code.
/// </summary>
internal sealed record AdditionalProjectSettings
{
    /// <summary>
    /// Path to the project file; the entry is skipped when empty.
    /// </summary>
    internal string? Path { get; init; }

    /// <summary>
    /// Names of the Bitwarden services that receive a reference to this project.
    /// </summary>
    internal string[] ReferencedBy { get; init; } = [];
}

/// <summary>
/// Settings for the SQL Server container.
/// </summary>
internal sealed record DatabaseSettings
{
    /// <summary>
    /// Full image reference, such as <c>mssql/server:2025-latest</c>.
    /// </summary>
    internal required string Image { get; init; }

    /// <summary>
    /// Host port the container publishes.
    /// </summary>
    internal required int Port { get; init; }

    /// <summary>
    /// The <c>sa</c> password in cloud mode.
    /// </summary>
    internal required string Password { get; init; }

    /// <summary>
    /// The <c>sa</c> password in self-hosted mode.
    /// </summary>
    internal required string SelfHostPassword { get; init; }
}

/// <summary>
/// File names of the PowerShell scripts the AppHost runs.
/// </summary>
internal sealed record ScriptSettings
{
    /// <summary>
    /// Applies database migrations.
    /// </summary>
    internal required string DbMigration { get; init; }

    /// <summary>
    /// Creates the Azurite blob containers, queues and tables.
    /// </summary>
    internal required string AzuriteSetup { get; init; }

    /// <summary>
    /// Copies <c>dev/secrets.json</c> into each project's user secrets.
    /// </summary>
    internal required string SecretsSetup { get; init; }
}

/// <summary>
/// A container started from a <c>name[:tag]</c> image reference.
/// </summary>
internal abstract record ContainerImageSettings
{
    /// <summary>
    /// Image reference in <c>name[:tag]</c> form.
    /// </summary>
    internal required string Image { get; init; }

    /// <summary>
    /// Splits <see cref="Image"/> into name and tag, defaulting the tag to <c>latest</c>.
    /// </summary>
    internal (string Name, string Tag) GetImageParts()
    {
        var parts = Image.Split(':');
        return (parts[0], parts.Length > 1 ? parts[1] : "latest");
    }
}

/// <summary>
/// A container that publishes one host port.
/// </summary>
internal sealed record ContainerSettings : ContainerImageSettings
{
    /// <summary>
    /// Host port the container publishes.
    /// </summary>
    internal required int Port { get; init; }
}

/// <summary>
/// Settings for the MailCatcher container.
/// </summary>
internal sealed record MailCatcherSettings : ContainerImageSettings
{
    /// <summary>
    /// Host port the Bitwarden services send SMTP mail to.
    /// </summary>
    internal required int SmtpPort { get; init; }

    /// <summary>
    /// Host port of the web inbox.
    /// </summary>
    internal required int WebPort { get; init; }
}

/// <summary>
/// Settings for the web vault npm watch build.
/// </summary>
internal sealed record WebFrontendSettings
{
    /// <summary>
    /// HTTPS port in cloud mode; self-hosted mode adds one.
    /// </summary>
    internal required int Port { get; init; }

    /// <summary>
    /// Base URL shown on the dashboard, without the port.
    /// </summary>
    internal required string Url { get; init; }
}
