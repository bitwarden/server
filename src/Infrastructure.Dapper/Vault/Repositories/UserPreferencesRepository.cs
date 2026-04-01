using System.Data;
using Bit.Core.Settings;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Vault.Repositories;

public class UserPreferencesRepository(string connectionString, string readOnlyConnectionString)
    : Repository<UserPreferences, Guid>(connectionString, readOnlyConnectionString), IUserPreferencesRepository
{
    public UserPreferencesRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
    }

    public async Task<UserPreferences?> GetByUserIdAsync(Guid userId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var result = await connection.QueryFirstOrDefaultAsync<UserPreferences>(
            $"[{Schema}].[UserPreferences_ReadByUserId]",
            new { UserId = userId },
            commandType: CommandType.StoredProcedure);

        return result;
    }

    public async Task DeleteByUserIdAsync(Guid userId)
    {
        var connection = new SqlConnection(ConnectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.ExecuteAsync(
                $"[{Schema}].[UserPreferences_DeleteByUserId]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);
        }
    }
}
