namespace Bit.AppHost.Configuration.Databases;

/// <summary>
/// The <c>Databases</c> section: which engines are in play, how each one runs, and the rules across engines.
/// An absent engine section binds to its defaults, which leave the engine <see cref="EngineMode.Disabled"/>.
/// </summary>
internal sealed record EngineLineup
{
    /// <summary>
    /// The SQL Server engine; it cannot be <see cref="EngineMode.Disabled"/>.
    /// </summary>
    internal ServerEngineSettings MsSql { get; init; } = new();

    /// <summary>
    /// The PostgreSQL engine.
    /// </summary>
    internal ServerEngineSettings Postgres { get; init; } = new();

    /// <summary>
    /// The MySQL engine; it cannot be enabled together with <see cref="MariaDb"/>.
    /// </summary>
    internal ServerEngineSettings MySql { get; init; } = new();

    /// <summary>
    /// The MariaDB engine, run through the MySQL integration; it cannot be enabled together with
    /// <see cref="MySql"/>.
    /// </summary>
    internal ServerEngineSettings MariaDb { get; init; } = new();

    /// <summary>
    /// The SQLite engine.
    /// </summary>
    internal SqliteSettings Sqlite { get; init; } = new();

    /// <summary>
    /// Checks the rules that depend on each engine's <c>Mode</c>, which <c>required</c> cannot express.
    /// </summary>
    /// <exception cref="InvalidOperationException">A mode is not allowed or a value its mode needs is empty.</exception>
    internal void Validate()
    {
        if (MsSql.Mode == EngineMode.Disabled)
        {
            throw new InvalidOperationException(
                $"{Key(nameof(MsSql), nameof(MsSql.Mode))} cannot be {EngineMode.Disabled}; use " +
                $"{EngineMode.Container}, or {EngineMode.External} for a SQL Server running outside Aspire.");
        }

        if (Sqlite.Mode == EngineMode.Container)
        {
            throw new InvalidOperationException(
                $"{Key(nameof(Sqlite), nameof(Sqlite.Mode))} cannot be {EngineMode.Container}: SQLite is the file " +
                $"named in dev/secrets.json. Use {EngineMode.External} to migrate it, or {EngineMode.Disabled}.");
        }

        if (MySql.Mode != EngineMode.Disabled && MariaDb.Mode != EngineMode.Disabled)
        {
            throw new InvalidOperationException(
                $"{Section(nameof(MySql))} and {Section(nameof(MariaDb))} cannot both be enabled: dev/migrate.ps1 " +
                $"migrates both through globalSettings:mySql:connectionString. Set one {nameof(MySql.Mode)} to " +
                $"{EngineMode.Disabled}.");
        }

        RequireContainerValues(nameof(MsSql), MsSql);
        RequireContainerValues(nameof(Postgres), Postgres);
        RequireContainerValues(nameof(MySql), MySql);
        RequireContainerValues(nameof(MariaDb), MariaDb);
    }

    private static void RequireContainerValues(string engine, ServerEngineSettings server)
    {
        if (server.Mode != EngineMode.Container)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(server.Image))
        {
            throw AppHostSettingsBinding.MissingConfiguration(Key(engine, nameof(server.Image)));
        }

        if (server.Port <= 0)
        {
            throw AppHostSettingsBinding.MissingConfiguration(Key(engine, nameof(server.Port)));
        }
    }

    private static string Section(string engine) => $"{nameof(AppHostSettings.Databases)}:{engine}";

    private static string Key(string engine, string setting) => $"{Section(engine)}:{setting}";
}
