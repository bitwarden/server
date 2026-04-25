using Bit.Core.Enums;
using Bit.Core.Repositories;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.IntegrationTest;

/// <summary>
/// Executes <see cref="DatabaseTransactionAction"/> delegates in integration tests.
/// For Dapper (SQL Server without EF), opens a connection and transaction.
/// For EF providers, invokes the delegate without connection parameters.
/// </summary>
public static class DatabaseTransactionActionTestHelper
{
    public static async Task ExecuteAsync(Database database, IEnumerable<DatabaseTransactionAction> actions)
    {
        if (database.Type == SupportedDatabaseProviders.SqlServer && !database.UseEf)
        {
            await using var connection = new SqlConnection(database.ConnectionString);
            await connection.OpenAsync();
            await using var transaction = connection.BeginTransaction();
            foreach (var action in actions)
            {
                await action(connection, transaction);
            }

            await transaction.CommitAsync();
        }
        else
        {
            foreach (var action in actions)
            {
                await action();
            }
        }
    }

    public static Task ExecuteAsync(Database database, DatabaseTransactionAction action)
        => ExecuteAsync(database, [action]);
}
