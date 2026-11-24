#nullable enable

using System.Data;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Auth.Repositories;

/// <inheritdoc cref="IConnectedAppRepository" />
public class ConnectedAppRepository : Repository<ConnectedApp, Guid>, IConnectedAppRepository
{
    public ConnectedAppRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public ConnectedAppRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    /// <inheritdoc />
    public async Task<ICollection<ConnectedApp>> GetManyByUserIdAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<ConnectedApp>(
                $"[{Schema}].[ConnectedApp_ReadByUserId]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    /// <inheritdoc />
    public DatabaseTransactionAction UpdateForKeyRotation(Guid userId, IEnumerable<ConnectedApp> connections)
    {
        return async (connection, transaction) =>
        {
            // Only the re-wrapped Key is persisted here; the rotation runs inside the caller's transaction.
            const string sql = @"
                UPDATE
                    [dbo].[ConnectedApp]
                SET
                    [Key] = @Key
                WHERE
                    [Id] = @Id
                    AND [UserId] = @UserId";

            foreach (var connectedApp in connections)
            {
                await connection.ExecuteAsync(
                    sql,
                    new { connectedApp.Id, connectedApp.Key, UserId = userId },
                    transaction: transaction,
                    commandType: CommandType.Text);
            }
        };
    }
}
