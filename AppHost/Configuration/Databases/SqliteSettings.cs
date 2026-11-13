namespace Bit.AppHost.Configuration.Databases;

/// <summary>
/// Settings for the SQLite engine. Its database is the file named in <c>dev/secrets.json</c>, so it never runs in
/// a container.
/// </summary>
internal sealed record SqliteSettings
{
    /// <summary>
    /// <see cref="EngineMode.External"/> runs the SQLite migration on start; <see cref="EngineMode.Container"/>
    /// is rejected.
    /// </summary>
    internal EngineMode Mode { get; init; }
}
