using Bit.AppHost.Configuration.Databases;

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
    /// One section per database engine, each saying whether the AppHost runs it, uses an external instance or
    /// ignores it.
    /// </summary>
    internal required EngineLineup Databases { get; init; }

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
