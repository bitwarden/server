using Bit.AppHost.Configuration;
using Bit.AppHost.Configuration.Databases;

namespace Bit.AppHost.Resources;

/// <summary>
/// Adds one <c>dev/migrate.ps1</c> run per database target. A target whose database runs in a container migrates
/// on start; the others start only on request.
/// </summary>
internal static class DatabaseMigrationResources
{
    private const string _resourcePrefix = "migrate-";

    private const string _selfHostSwitch = "-selfhost";

    extension(IDistributedApplicationBuilder builder)
    {
        /// <summary>
        /// Adds the migration resources: both SQL Server targets always, PostgreSQL, MySQL and MariaDB unless
        /// Disabled, and SQLite when External.
        /// </summary>
        /// <param name="databases">The containers a migration waits for before it runs.</param>
        /// <param name="secretsSetup">The secrets setup every migration waits to finish.</param>
        /// <returns>The migrations that start with the AppHost; the explicit-start ones are left out.</returns>
        internal IReadOnlyList<IResourceBuilder<ExecutableResource>> ConfigureDatabaseMigrations(
            DatabaseContainers databases, IResourceBuilder<ExecutableResource> secretsSetup)
        {
            var settings = builder.Settings;
            var lineup = settings.Databases;
            var msSqlInContainer = databases.MsSql is not null;

            List<(string Name, string? Switch, IResourceBuilder<IResourceWithConnectionString>? Container, bool AutoStart)> targets =
            [
                (ResourceName(Engine.MsSql), null, databases.MsSql, msSqlInContainer && !settings.SelfHost),
                (ResourceName(Engine.MsSql) + _selfHostSwitch, _selfHostSwitch, databases.MsSql,
                    msSqlInContainer && settings.SelfHost)
            ];

            if (lineup.Postgres.Mode != EngineMode.Disabled)
            {
                targets.Add(MigrationTarget(Engine.Postgres, databases.Postgres));
            }

            if (lineup.MySql.Mode != EngineMode.Disabled)
            {
                targets.Add(MigrationTarget(Engine.MySql, databases.MySql));
            }

            if (lineup.MariaDb.Mode != EngineMode.Disabled)
            {
                targets.Add(MigrationTarget(Engine.MariaDb, databases.MariaDb));
            }

            if (lineup.Sqlite.Mode == EngineMode.External)
            {
                targets.Add((ResourceName(Engine.Sqlite), Engine.Sqlite.MigrationSwitch, null, AutoStart: true));
            }

            var autoStarted = new List<IResourceBuilder<ExecutableResource>>();
            foreach (var (name, migrationSwitch, container, autoStart) in targets)
            {
                string[] args = migrationSwitch is null
                    ? ["-File", settings.Scripts.DbMigration]
                    : ["-File", settings.Scripts.DbMigration, migrationSwitch];
                var migration = builder
                    .AddExecutable(name, "pwsh", settings.WorkingDirectory, args)
                    .WaitForCompletion(secretsSetup)
                    .ExcludeFromManifest();

                if (container is not null)
                {
                    migration.WaitFor(container);
                }

                if (autoStart)
                {
                    autoStarted.Add(migration);
                }
                else
                {
                    migration.WithExplicitStart();
                }
            }

            return autoStarted;
        }
    }

    private static (string Name, string? Switch, IResourceBuilder<IResourceWithConnectionString>? Container, bool AutoStart)
        MigrationTarget(Engine engine, IResourceBuilder<IResourceWithConnectionString>? container) =>
        (ResourceName(engine), engine.MigrationSwitch, container, AutoStart: container is not null);

    private static string ResourceName(Engine engine) => _resourcePrefix + engine.Name;
}
