using System.Data;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Dirt.Repositories;

public class OrganizationDeleteTaskRepository : BaseRepository, IOrganizationDeleteTaskRepository
{
    private const int LeaseDurationMinutes = 10;
    private const int MaxFailureCount = 5;

    public OrganizationDeleteTaskRepository(GlobalSettings globalSettings)
        : base(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public async Task CreateAsync(OrganizationDeleteTask task)
    {
        task.SetNewId();
        using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            "[dbo].[OrganizationDeleteTask_Create]",
            new { task.Id, task.OrganizationId, task.TaskType, task.CreationDate },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<OrganizationDeleteTask?> ClaimNextPendingAsync()
    {
        var now = DateTime.UtcNow;
        using var connection = new SqlConnection(ConnectionString);
        return await connection.QuerySingleOrDefaultAsync<OrganizationDeleteTask>(
            "[dbo].[OrganizationDeleteTask_UpdateClaimNextPending]",
            new { Now = now, StaleLeaseThreshold = now.AddMinutes(-LeaseDurationMinutes), MaxFailureCount },
            commandType: CommandType.StoredProcedure);
    }

    public async Task UpdateProgressAsync(Guid id, long delta)
    {
        using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            "[dbo].[OrganizationDeleteTask_UpdateProgress]",
            new { Id = id, Delta = delta, Now = DateTime.UtcNow },
            commandType: CommandType.StoredProcedure);
    }

    public async Task UpdateErrorAsync(Guid id, string message)
    {
        using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            "[dbo].[OrganizationDeleteTask_UpdateError]",
            new { Id = id, Message = message, Now = DateTime.UtcNow },
            commandType: CommandType.StoredProcedure);
    }

    public async Task UpdateCompletedAsync(Guid id)
    {
        using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            "[dbo].[OrganizationDeleteTask_UpdateCompleted]",
            new { Id = id, Now = DateTime.UtcNow },
            commandType: CommandType.StoredProcedure);
    }
}
