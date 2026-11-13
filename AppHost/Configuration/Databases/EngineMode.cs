namespace Bit.AppHost.Configuration.Databases;

/// <summary>
/// How the AppHost treats one database engine.
/// </summary>
internal enum EngineMode
{
    /// <summary>
    /// The AppHost starts nothing and creates no migration resource. An absent <c>Mode</c> binds to this.
    /// </summary>
    Disabled,

    /// <summary>
    /// The AppHost runs the engine in a persistent container and migrates it on start.
    /// </summary>
    Container,

    /// <summary>
    /// The engine runs outside Aspire, such as a compose container or a SQLite file. The AppHost starts no container
    /// but still adds the engine's migration resource.
    /// </summary>
    External
}
