using System.Data;
using Bit.Core.Settings;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Vault.Repositories;

public class SecurityTaskRepository : Repository<SecurityTask, Guid>, ISecurityTaskRepository
{
    public SecurityTaskRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public SecurityTaskRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    /// <inheritdoc />
    public async Task<ICollection<SecurityTask>> GetManyByUserIdStatusAsync(Guid userId,
        SecurityTaskStatus? status = null)
    {
        await using var connection = new SqlConnection(ConnectionString);

        var results = await connection.QueryAsync<SecurityTask>(
            $"[{Schema}].[SecurityTask_ReadByUserIdStatus]",
            new { UserId = userId, Status = status },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }

    /// <inheritdoc />
    public async Task<ICollection<SecurityTask>> CreateManyAsync(IEnumerable<SecurityTask> tasks)
    {
        var tasksList = tasks?.ToList();
        if (tasksList is null || tasksList.Count == 0)
        {
            return Array.Empty<SecurityTask>();
        }

        foreach (var task in tasksList)
        {
            task.SetNewId();
        }

        var securityTasksTvp = tasksList.ToTvp();
        await using var connection = new SqlConnection(ConnectionString);

        var results = await connection.ExecuteAsync(
            $"[{Schema}].[{Table}_CreateMany]",
            new { SecurityTasksInput = securityTasksTvp },
            commandType: CommandType.StoredProcedure);

        return tasksList;
    }
}
