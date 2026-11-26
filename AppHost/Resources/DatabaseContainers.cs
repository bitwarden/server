namespace Bit.AppHost.Resources;

/// <summary>
/// The vault databases the AppHost runs in containers. A member is <c>null</c> when its engine is
/// <c>External</c> or <c>Disabled</c>.
/// </summary>
internal sealed record DatabaseContainers
{
    /// <summary>
    /// The SQL Server vault database.
    /// </summary>
    internal IResourceBuilder<SqlServerDatabaseResource>? MsSql { get; init; }

    /// <summary>
    /// The PostgreSQL vault database.
    /// </summary>
    internal IResourceBuilder<PostgresDatabaseResource>? Postgres { get; init; }

    /// <summary>
    /// The MySQL vault database.
    /// </summary>
    internal IResourceBuilder<MySqlDatabaseResource>? MySql { get; init; }

    /// <summary>
    /// The MariaDB vault database, run through the MySQL integration.
    /// </summary>
    internal IResourceBuilder<MySqlDatabaseResource>? MariaDb { get; init; }

    /// <summary>
    /// Every vault database that runs in a container.
    /// </summary>
    internal IEnumerable<IResourceBuilder<IResourceWithConnectionString>> All =>
        new IResourceBuilder<IResourceWithConnectionString>?[] { MsSql, Postgres, MySql, MariaDb }
            .OfType<IResourceBuilder<IResourceWithConnectionString>>();
}
