namespace Bit.Infrastructure.Dapper.Repositories;

#nullable enable

internal static class DatabaseExceptionHelpers
{
    /// <summary>
    /// Determines if an exception represents a SQL Server duplicate key constraint violation.
    /// </summary>
    public static bool IsDuplicateKeyException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception is Microsoft.Data.SqlClient.SqlException { Errors: not null } msEx &&
               msEx.Errors.Cast<Microsoft.Data.SqlClient.SqlError>().Any(error => error.Number == 2627);
    }
}
