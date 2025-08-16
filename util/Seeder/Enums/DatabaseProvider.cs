#nullable enable

namespace Bit.Seeder.Enums;

/// <summary>
/// Specifies the database provider to use for seeding operations
/// </summary>
public enum DatabaseProvider
{
    /// <summary>
    /// Auto-detect from configuration
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Microsoft SQL Server
    /// </summary>
    SqlServer = 1,

    /// <summary>
    /// PostgreSQL
    /// </summary>
    PostgreSQL = 2,

    /// <summary>
    /// MySQL/MariaDB
    /// </summary>
    MySQL = 3,

    /// <summary>
    /// SQLite
    /// </summary>
    SQLite = 4
}
