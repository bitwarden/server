﻿using System.Data;
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
    public async Task<ICollection<Guid>> CreateManyAsync(IEnumerable<SecurityTask> tasks)
    {
        if (tasks?.Any() != true)
        {
            return Array.Empty<Guid>();
        }

        var tasksList = tasks.ToList();
        foreach (var task in tasksList)
        {
            task.SetNewId();
        }

        var securityTasksTvp = tasksList.ToTvp();
        await using var connection = new SqlConnection(ConnectionString);

        var results = await connection.ExecuteAsync(
            $"[{Schema}].[SecurityTask_CreateMany]",
            new { SecurityTasksInput = securityTasksTvp },
            commandType: CommandType.StoredProcedure);

        return tasksList.Select(t => t.Id).ToList();
    }
}
