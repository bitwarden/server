using System.Data;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Dirt.Repositories;

public class OrganizationEventCleanupRepository : BaseRepository, IOrganizationEventCleanupRepository
{
    public OrganizationEventCleanupRepository(GlobalSettings globalSettings)
        : base(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public async Task CreateAsync(OrganizationEventCleanup cleanup)
    {
        cleanup.SetNewId();
        using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            "[dbo].[OrganizationEventCleanup_Create]",
            new { cleanup.Id, cleanup.OrganizationId, cleanup.CreationDate },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<OrganizationEventCleanup?> ClaimNextPendingAsync()
    {
        using var connection = new SqlConnection(ConnectionString);
        return await connection.QuerySingleOrDefaultAsync<OrganizationEventCleanup>(
            "[dbo].[OrganizationEventCleanup_ClaimNextPending]",
            new { Now = DateTime.UtcNow },
            commandType: CommandType.StoredProcedure);
    }

    public async Task UpdateProgressAsync(Guid id, long delta)
    {
        using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            "[dbo].[OrganizationEventCleanup_UpdateProgress]",
            new { Id = id, Delta = delta, Now = DateTime.UtcNow },
            commandType: CommandType.StoredProcedure);
    }

    public async Task UpdateErrorAsync(Guid id, string message)
    {
        using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            "[dbo].[OrganizationEventCleanup_UpdateError]",
            new { Id = id, Message = message, Now = DateTime.UtcNow },
            commandType: CommandType.StoredProcedure);
    }

    public async Task UpdateCompletedAsync(Guid id)
    {
        using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            "[dbo].[OrganizationEventCleanup_UpdateCompleted]",
            new { Id = id, Now = DateTime.UtcNow },
            commandType: CommandType.StoredProcedure);
    }
}
