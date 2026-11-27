namespace Bit.AppHost.Configuration.Databases;

/// <summary>
/// A database engine the Bitwarden server supports and <c>dev/migrate.ps1</c> can migrate.
/// </summary>
internal enum Engine
{
    /// <summary>
    /// Microsoft SQL Server.
    /// </summary>
    MsSql,

    /// <summary>
    /// PostgreSQL.
    /// </summary>
    Postgres,

    /// <summary>
    /// MySQL.
    /// </summary>
    MySql,

    /// <summary>
    /// MariaDB, which shares MySQL's connection string and migrations.
    /// </summary>
    MariaDb,

    /// <summary>
    /// SQLite, a file database.
    /// </summary>
    Sqlite
}
