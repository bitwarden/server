namespace Bit.AppHost.Configuration.Databases;

/// <summary>
/// Settings for a database engine that runs as a server, unlike SQLite's file, either in a container or outside
/// Aspire. What must be set depends on <see cref="Mode"/>.
/// </summary>
internal sealed record ServerEngineSettings : ContainerImageSettings
{
    /// <summary>
    /// Image reference in <c>name[:tag]</c> form; must be set in <see cref="EngineMode.Container"/> mode.
    /// </summary>
    internal override string Image { get; init; } = "";

    /// <summary>
    /// Whether the AppHost runs the engine, uses an external instance or ignores it.
    /// </summary>
    internal EngineMode Mode { get; init; }

    /// <summary>
    /// Host port the container publishes; must match the port in the engine's <c>dev/secrets.json</c> connection
    /// string.
    /// </summary>
    internal int Port { get; init; }

    /// <summary>
    /// Container name instead of Aspire's per-checkout generated one; empty keeps Aspire's.
    /// </summary>
    internal string ContainerName { get; init; } = "";

    /// <summary>
    /// Data volume name instead of Aspire's per-checkout generated one; empty keeps Aspire's.
    /// </summary>
    internal string VolumeName { get; init; } = "";
}
