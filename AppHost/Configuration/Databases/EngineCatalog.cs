namespace Bit.AppHost.Configuration.Databases;

/// <summary>
/// Gives each <see cref="Engine"/> its resource name, password parameter, <c>dev/migrate.ps1</c> switch and
/// <c>dev/secrets.json</c> connection string key.
/// </summary>
/// <remarks>
/// The switch has no discard arm on purpose: CS8509 is a build error in this repo, so an engine added without an
/// entry here fails to compile.
/// </remarks>
internal static class EngineCatalog
{
    extension(Engine engine)
    {
        /// <summary>
        /// The Aspire resource name of the engine's container, such as <c>postgres</c>.
        /// </summary>
        internal string Name => engine.Entry.Name;

        /// <summary>
        /// The name of the secret Aspire parameter holding the container's admin password; <c>null</c> for SQLite,
        /// which never runs in a container.
        /// </summary>
        internal string? PasswordParameterName => engine.Entry.PasswordParameterName;

        /// <summary>
        /// The <c>dev/migrate.ps1</c> switch that selects the engine; <c>null</c> for SQL Server, the script's
        /// default.
        /// </summary>
        internal string? MigrationSwitch => engine.Entry.MigrationSwitch;

        /// <summary>
        /// The <c>dev/secrets.json</c> key of the engine's connection string. MySQL and MariaDB share one.
        /// </summary>
        internal string ConnectionStringKey => engine.Entry.ConnectionStringKey;

        private (string Name, string? PasswordParameterName, string? MigrationSwitch, string ConnectionStringKey) Entry =>
            engine switch
            {
                Engine.MsSql => ("mssql", "mssql-password", null, "globalSettings:sqlServer:connectionString"),
                Engine.Postgres => ("postgres", "postgres-password", "-postgres", "globalSettings:postgreSql:connectionString"),
                Engine.MySql => ("mysql", "mysql-password", "-mysql", "globalSettings:mySql:connectionString"),
                Engine.MariaDb => ("mariadb", "mariadb-password", "-mariadb", "globalSettings:mySql:connectionString"),
                Engine.Sqlite => ("sqlite", null, "-sqlite", "globalSettings:sqlite:connectionString")
            };
    }
}
