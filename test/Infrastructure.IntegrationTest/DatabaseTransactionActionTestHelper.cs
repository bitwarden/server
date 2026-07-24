using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.IntegrationTest;

/// <summary>
/// Executes <see cref="DatabaseTransactionAction"/> delegates in integration tests.
/// Opens a connection and transaction appropriate for the database provider, executes the actions, and commits.
/// </summary>
public static class DatabaseTransactionActionTestHelper
{
    public static Task ExecuteAsync(Database database, DatabaseTransactionAction action,
        IServiceProvider serviceProvider)
        => ExecuteAsync(database, [action], serviceProvider);

    public static async Task ExecuteAsync(Database database, IEnumerable<DatabaseTransactionAction> actions,
        IServiceProvider serviceProvider)
    {
        var isDapper = database.Type == SupportedDatabaseProviders.SqlServer && !database.UseEf;
        var connection = isDapper
            ? new SqlConnection(database.ConnectionString)
            : serviceProvider.GetRequiredService<DatabaseContext>().Database.GetDbConnection();

        try
        {
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            foreach (var action in actions)
            {
                await action(connection, transaction);
            }

            await transaction.CommitAsync();
        }
        finally
        {
            if (isDapper)
            {
                await connection.DisposeAsync();
            }
        }
    }
}
