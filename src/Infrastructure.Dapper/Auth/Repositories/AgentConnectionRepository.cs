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

/// <inheritdoc cref="IAgentConnectionRepository" />
public class AgentConnectionRepository : Repository<AgentConnection, Guid>, IAgentConnectionRepository
{
    public AgentConnectionRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public AgentConnectionRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    /// <inheritdoc />
    public async Task<ICollection<AgentConnection>> GetManyByUserIdAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<AgentConnection>(
                $"[{Schema}].[AgentConnection_ReadByUserId]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    /// <inheritdoc />
    public DatabaseTransactionAction UpdateForKeyRotation(Guid userId, IEnumerable<AgentConnection> connections)
    {
        return async (connection, transaction) =>
        {
            // Only the re-wrapped Key is persisted here; the rotation runs inside the caller's transaction.
            const string sql = @"
                UPDATE
                    [dbo].[AgentConnection]
                SET
                    [Key] = @Key
                WHERE
                    [Id] = @Id
                    AND [UserId] = @UserId";

            foreach (var agentConnection in connections)
            {
                await connection.ExecuteAsync(
                    sql,
                    new { agentConnection.Id, agentConnection.Key, UserId = userId },
                    transaction: transaction,
                    commandType: CommandType.Text);
            }
        };
    }
}
