using System.Data;
using System.Text.Json;
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
    public async Task<ICollection<SecurityTask>> GetManyByOrganizationIdStatusAsync(Guid organizationId,
        SecurityTaskStatus? status = null)
    {
        await using var connection = new SqlConnection(ConnectionString);

        var results = await connection.QueryAsync<SecurityTask>(
            $"[{Schema}].[SecurityTask_ReadByOrganizationIdStatus]",
            new { OrganizationId = organizationId, Status = status },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }

    /// <inheritdoc />
    public async Task<SecurityTaskMetrics> GetTaskMetricsAsync(Guid organizationId)
    {
        await using var connection = new SqlConnection(ConnectionString);

        var result = await connection.QueryAsync<SecurityTaskMetrics>(
            $"[{Schema}].[SecurityTask_ReadByOrganizationIdMetrics]",
            new { OrganizationId = organizationId },
            commandType: CommandType.StoredProcedure);

        return result.FirstOrDefault() ?? new SecurityTaskMetrics(0, 0);
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

        var tasksJson = JsonSerializer.Serialize(tasksList);

        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            $"[{Schema}].[{Table}_CreateMany]",
            new { SecurityTasksJson = tasksJson },
            commandType: CommandType.StoredProcedure);

        return tasksList;
    }
}
