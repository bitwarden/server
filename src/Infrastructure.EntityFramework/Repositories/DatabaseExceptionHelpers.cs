namespace Bit.Infrastructure.EntityFramework.Repositories;

#nullable enable

internal static class DatabaseExceptionHelpers
{
    /// <summary>
    /// Determines if a DbUpdateException represents a duplicate key constraint violation.
    /// Works with MySQL, SQL Server, PostgreSQL, and SQLite.
    /// </summary>
    public static bool IsDuplicateKeyException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        switch (exception)
        {
            // MySQL
            case MySqlConnector.MySqlException myEx:
                return myEx.ErrorCode == MySqlConnector.MySqlErrorCode.DuplicateKeyEntry;
            // SQL Server
            case Microsoft.Data.SqlClient.SqlException msEx:
                return msEx.Errors != null &&
                       msEx.Errors.Cast<Microsoft.Data.SqlClient.SqlError>().Any(error => error.Number == 2627);
            // PostgreSQL
            case Npgsql.PostgresException pgEx:
                return pgEx.SqlState == "23505";
            // SQLite
            case Microsoft.Data.Sqlite.SqliteException liteEx:
                return liteEx is { SqliteErrorCode: 19, SqliteExtendedErrorCode: 1555 };
            default:
                return false;
        }
    }
}
