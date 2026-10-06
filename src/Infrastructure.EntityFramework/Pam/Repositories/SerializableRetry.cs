#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Repositories;

internal static class SerializableRetry
{
    private const int MaxAttempts = 3;

    public static async Task<T> RunAsync<T>(Func<Task<T>> write)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await write();
            }
            catch (Exception e) when (attempt < MaxAttempts && IsSerializationFailure(e))
            {
            }
        }
    }

    private static bool IsSerializationFailure(Exception e) => e switch
    {
        Npgsql.PostgresException pg => pg.SqlState is "40001" or "40P01",
        MySqlConnector.MySqlException my => my.ErrorCode is MySqlConnector.MySqlErrorCode.LockDeadlock
            or MySqlConnector.MySqlErrorCode.LockWaitTimeout,
        Microsoft.Data.SqlClient.SqlException ms => ms.Errors
            .Cast<Microsoft.Data.SqlClient.SqlError>()
            .Any(error => error.Number is 1205),
        Microsoft.Data.Sqlite.SqliteException lite => lite.SqliteErrorCode is 5 or 6,
        _ => e.InnerException is not null && IsSerializationFailure(e.InnerException),
    };
}
